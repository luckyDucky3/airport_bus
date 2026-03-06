using System.Text.Json;
using Confluent.Kafka;
using Infrastructure.Integrations;
using Infrastructure.Kafka.Contracts;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Domain;

namespace Infrastructure.Kafka;

public sealed class SimTimeTickConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<SimTimeTickConsumer> logger
) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId + "-sim",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_options.SimTopic);

        logger.LogInformation("Sim tick consumer started. Topic={Topic}, GroupId={GroupId}", _options.SimTopic, config.GroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                {
                    continue;
                }

                if (!KafkaEventParser.TryParseSimTick(result.Message.Value, out var eventId, out var tick))
                {
                    consumer.Commit(result);
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BusDbContext>();
                var ground = scope.ServiceProvider.GetRequiredService<IGroundClient>();

                await ProcessTickAsync(db, ground, eventId, tick, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error in sim consumer");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in sim consumer");
            }
        }

        consumer.Close();
    }

    private async Task ProcessTickAsync(BusDbContext db, IGroundClient groundClient, Guid eventId, SimTimeTickPayload tick, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var processed = await TryInsertProcessedEventAsync(db, eventId, ct);
        if (!processed)
        {
            await tx.CommitAsync(ct);
            return;
        }

        var runtime = await db.Runtime.FirstOrDefaultAsync(x => x.RuntimeId == "global", ct);
        if (runtime is null)
        {
            runtime = new BusRuntimeEntity
            {
                RuntimeId = "global",
                LastSimTime = null,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Runtime.Add(runtime);
            await db.SaveChangesAsync(ct);
        }

        if (tick.Paused)
        {
            runtime.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return;
        }

        if (runtime.LastSimTime is not null && tick.SimTime <= runtime.LastSimTime)
        {
            await tx.CommitAsync(ct);
            return;
        }

        var completedRouteIds = new List<Guid>();

        var runningTrips = await db.Trips
            .Include(x => x.Runtime)
            .Include(x => x.Passengers)
            .Include(x => x.Task)
            .Where(x => x.Status == StatusValues.TripStateMovingToPickup || x.Status == StatusValues.TripStateLoading || x.Status == StatusValues.TripStateMovingToPlane)
            .ToListAsync(ct);

        foreach (var trip in runningTrips)
        {
            if (trip.Runtime is null)
            {
                trip.Runtime = new BusTripRuntimeEntity
                {
                    TripId = trip.TripId,
                    RemainingMinutes = trip.Task?.TripDurationMinutes ?? 1,
                    StartSimTime = tick.SimTime,
                    FinishSimTime = null
                };
            }

            trip.Runtime.RemainingMinutes -= tick.TickMinutes;
            if (trip.Runtime.RemainingMinutes > 0)
            {
                trip.UpdatedAt = DateTimeOffset.UtcNow;
                continue;
            }

            trip.Runtime.RemainingMinutes = 0;
            trip.Runtime.FinishSimTime = tick.SimTime;
            trip.Status = StatusValues.TripStateDone;
            trip.DoneAt = tick.SimTime;
            trip.UpdatedAt = DateTimeOffset.UtcNow;

            var bus = await db.Buses.FirstOrDefaultAsync(x => x.BusId == trip.BusId, ct);
            if (bus is not null)
            {
                bus.State = StatusValues.BusStateFree;
                bus.RouteId = null;
                bus.LocationNode = trip.ToNode;
                bus.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (trip.Task is not null)
            {
                trip.Task.TripsDone += 1;
                trip.Task.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (trip.RouteId is not null)
            {
                completedRouteIds.Add(trip.RouteId.Value);
            }

            db.OutboxEvents.Add(CreatePassengersDeliveredOutbox(_options.BoardTopic, trip, tick.SimTime));
        }

        runtime.LastSimTime = tick.SimTime;
        runtime.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        foreach (var routeId in completedRouteIds.Distinct())
        {
            await groundClient.ReleaseAsync(routeId, ct);
        }
    }

    private static async Task<bool> TryInsertProcessedEventAsync(BusDbContext db, Guid eventId, CancellationToken ct)
    {
        try
        {
            db.ProcessedEvents.Add(new ProcessedEventEntity
            {
                EventId = eventId,
                ProcessedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private static OutboxEventEntity CreatePassengersDeliveredOutbox(string boardTopic, BusTripEntity trip, DateTimeOffset simTime)
    {
        var body = new
        {
            eventId = Guid.NewGuid(),
            type = "bus.passengers.delivered",
            payload = new
            {
                taskId = trip.TaskId,
                tripId = trip.TripId,
                planeId = trip.PlaneId,
                flightId = trip.FlightId,
                passengerIds = trip.Passengers.Select(x => x.PassengerId).ToArray(),
                deliveredSimTime = simTime
            }
        };

        return new OutboxEventEntity
        {
            EventId = Guid.NewGuid(),
            Topic = boardTopic,
            EventType = "bus.passengers.delivered",
            EventKey = trip.TaskId,
            PayloadJson = JsonSerializer.Serialize(body),
            CreatedAt = DateTimeOffset.UtcNow,
            PublishAttempts = 0
        };
    }
}
