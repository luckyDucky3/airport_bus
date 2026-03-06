using System.Text.Json;
using Infrastructure.Integrations;
using Infrastructure.Kafka;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Domain;

namespace Infrastructure.Workers;

public sealed class QueuedJobsWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    IOptions<GroundOptions> groundOptions,
    ILogger<QueuedJobsWorker> logger
) : BackgroundService
{
    private readonly KafkaOptions _kafkaOptions = kafkaOptions.Value;
    private readonly GroundOptions _groundOptions = groundOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BusDbContext>();
                var passengers = scope.ServiceProvider.GetRequiredService<IPassengersClient>();
                var ground = scope.ServiceProvider.GetRequiredService<IGroundClient>();

                var jobs = await db.Jobs
                    .Include(x => x.Runtime)
                    .Where(x => x.Status == StatusValues.JobQueued || x.Status == StatusValues.JobRunning)
                    .OrderBy(x => x.CreatedAt)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var job in jobs)
                {
                    await ProcessJobAsync(db, passengers, ground, job, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "QueuedJobsWorker failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessJobAsync(
        BusDbContext db,
        IPassengersClient passengersClient,
        IGroundClient groundClient,
        BusJobEntity job,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        if (job.Runtime is null)
        {
            job.Runtime = new BusJobRuntimeEntity
            {
                TaskId = job.TaskId,
                PickupCompleted = false,
                UpdatedAt = now
            };
        }

        if (string.IsNullOrWhiteSpace(job.BusId))
        {
            var freeBus = await db.Buses
                .OrderBy(x => x.UpdatedAt)
                .FirstOrDefaultAsync(x => x.State == StatusValues.BusStateFree, ct);

            if (freeBus is null)
            {
                await db.SaveChangesAsync(ct);
                return;
            }

            job.BusId = freeBus.BusId;
            job.UpdatedAt = now;
        }

        var bus = await db.Buses.FirstOrDefaultAsync(x => x.BusId == job.BusId, ct);
        if (bus is null)
        {
            return;
        }

        var runningTrips = await db.Trips
            .Where(x => x.TaskId == job.TaskId && (x.Status == StatusValues.TripStateMovingToPickup || x.Status == StatusValues.TripStateLoading || x.Status == StatusValues.TripStateMovingToPlane))
            .CountAsync(ct);

        var createdTrip = await db.Trips
            .Include(x => x.Passengers)
            .Include(x => x.Runtime)
            .Where(x => x.TaskId == job.TaskId && x.Status == StatusValues.TripStateCreated)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (createdTrip is null && !job.Runtime.PickupCompleted)
        {
            createdTrip = new BusTripEntity
            {
                TripId = Guid.NewGuid(),
                TaskId = job.TaskId,
                BusId = job.BusId!,
                PlaneId = job.PlaneId,
                FlightId = job.FlightId,
                Status = StatusValues.TripStateCreated,
                FromNode = job.FromNode,
                ToNode = job.ToNode,
                CreatedAt = now,
                UpdatedAt = now,
                Runtime = new BusTripRuntimeEntity
                {
                    TripId = Guid.Empty,
                    RemainingMinutes = job.TripDurationMinutes,
                    StartSimTime = null,
                    FinishSimTime = null
                }
            };
            createdTrip.Runtime!.TripId = createdTrip.TripId;

            db.Trips.Add(createdTrip);
            job.TripsPlanned += 1;
            job.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }

        if (createdTrip is not null && createdTrip.Passengers.Count == 0 && !job.Runtime.PickupCompleted)
        {
            var pickup = await passengersClient.PickupAsync(new PickupRequest(job.FlightId, createdTrip.TripId, bus.Capacity), ct);
            if (pickup is null)
            {
                return;
            }

            if (pickup.PickedCount == 0)
            {
                job.Runtime.PickupCompleted = true;
                job.Runtime.UpdatedAt = now;

                db.Trips.Remove(createdTrip);
                job.TripsPlanned -= 1;
                job.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                createdTrip = null;
            }
            else
            {
                foreach (var passengerId in pickup.PassengerIds)
                {
                    db.TripPassengers.Add(new BusTripPassengerEntity
                    {
                        TripId = createdTrip.TripId,
                        PassengerId = passengerId
                    });
                }

                job.TotalPassengers += pickup.PickedCount;
                job.Status = StatusValues.JobRunning;
                job.UpdatedAt = now;
                createdTrip.UpdatedAt = now;
                await db.SaveChangesAsync(ct);

                createdTrip = await db.Trips
                    .Include(x => x.Passengers)
                    .Include(x => x.Runtime)
                    .FirstAsync(x => x.TripId == createdTrip.TripId, ct);
            }
        }

        if (createdTrip is not null && createdTrip.Passengers.Count > 0 && createdTrip.RouteId is null)
        {
            var reserve = await groundClient.ReserveAsync(new ReserveRouteRequest(
                createdTrip.TripId,
                bus.BusId,
                "bus",
                createdTrip.FromNode,
                createdTrip.ToNode,
                _groundOptions.RouteTtlMinutes
            ), ct);

            if (reserve.IsSuccess)
            {
                createdTrip.RouteId = reserve.Response!.RouteId;
                createdTrip.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
            }
            else
            {
                return;
            }
        }

        if (createdTrip is not null && createdTrip.Passengers.Count > 0 && createdTrip.RouteId is not null && bus.State == StatusValues.BusStateFree && runningTrips == 0)
        {
            createdTrip.Status = StatusValues.TripStateMovingToPickup;
            createdTrip.UpdatedAt = now;
            if (createdTrip.Runtime is not null && createdTrip.Runtime.StartSimTime is null)
            {
                createdTrip.Runtime.StartSimTime = now;
            }

            bus.State = StatusValues.BusStateMoving;
            bus.RouteId = createdTrip.RouteId;
            bus.LocationNode = createdTrip.FromNode;
            bus.UpdatedAt = now;
            job.Status = StatusValues.JobRunning;
            job.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }

        if (job.Runtime.PickupCompleted)
        {
            var activeTrips = await db.Trips
                .Where(x => x.TaskId == job.TaskId && x.Status != StatusValues.TripStateDone)
                .CountAsync(ct);

            if (activeTrips == 0 && job.Status != StatusValues.JobDone)
            {
                job.Status = StatusValues.JobDone;
                job.UpdatedAt = now;

                db.OutboxEvents.Add(CreateTaskCompletedOutbox(job, now));
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private OutboxEventEntity CreateTaskCompletedOutbox(BusJobEntity job, DateTimeOffset now)
    {
        var body = new
        {
            eventId = Guid.NewGuid(),
            type = "handling.task.completed",
            payload = new
            {
                taskId = job.TaskId,
                taskType = "bus",
                planeId = job.PlaneId,
                flightId = job.FlightId,
                result = new
                {
                    success = true,
                    tripsTotal = job.TripsPlanned,
                    passengersDelivered = job.TotalPassengers,
                    finishedSimTime = now
                }
            }
        };

        return new OutboxEventEntity
        {
            EventId = Guid.NewGuid(),
            Topic = _kafkaOptions.HandlingTopic,
            EventType = "handling.task.completed",
            EventKey = job.TaskId,
            PayloadJson = JsonSerializer.Serialize(body),
            CreatedAt = now,
            PublishAttempts = 0
        };
    }
}
