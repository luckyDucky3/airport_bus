using System.Text.Json;
using Models.Domain;
using Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Kafka;

public sealed class HandlingTaskCreatedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<HandlingTaskCreatedConsumer> logger
) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_options.Topic);

        logger.LogInformation("Kafka consumer started. Topic={Topic}, GroupId={GroupId}", _options.Topic, _options.GroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(stoppingToken);
                if (consumeResult?.Message?.Value is null)
                {
                    continue;
                }

                var message = ParseMessage(consumeResult.Message.Value);
                if (message is null)
                {
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BusDbContext>();

                await ProcessMessageAsync(db, message, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in Kafka consumer");
            }
        }

        consumer.Close();
    }

    private HandlingTaskCreatedMessage? ParseMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var taskId = GetString(root, "taskId", "task_id");
            var planeId = GetString(root, "planeId", "plane_id");
            var flightId = GetString(root, "flightId", "flight_id");
            var fromNode = GetString(root, "fromNode", "from_node");
            var toNode = GetString(root, "toNode", "to_node");
            var duration = GetInt(root, "tripDurationMinutes", "trip_duration_minutes");
            var totalPassengers = GetInt(root, "totalPassengers", "total_passengers");

            if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(planeId) || string.IsNullOrWhiteSpace(flightId)
                || string.IsNullOrWhiteSpace(fromNode) || string.IsNullOrWhiteSpace(toNode)
                || duration is null || duration <= 0 || totalPassengers is null || totalPassengers < 0)
            {
                logger.LogWarning("Invalid handling.task.created payload: {Json}", json);
                return null;
            }

            return new HandlingTaskCreatedMessage
            {
                TaskId = taskId,
                HandlingId = GetString(root, "handlingId", "handling_id"),
                PlaneId = planeId,
                FlightId = flightId,
                FromNode = fromNode,
                ToNode = toNode,
                TripDurationMinutes = duration.Value,
                TotalPassengers = totalPassengers.Value,
                PassengerIds = GetStringArray(root, "passengerIds", "passenger_ids")
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse handling.task.created JSON");
            return null;
        }
    }

    private async Task ProcessMessageAsync(BusDbContext db, HandlingTaskCreatedMessage msg, CancellationToken ct)
    {
        var existing = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.TaskId == msg.TaskId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Task {TaskId} already exists. Skip.", msg.TaskId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var bus = await db.Buses
            .Where(x => x.State == StatusValues.BusStateFree)
            .OrderBy(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var job = new BusJobEntity
        {
            TaskId = msg.TaskId,
            HandlingId = msg.HandlingId,
            PlaneId = msg.PlaneId,
            FlightId = msg.FlightId,
            FromNode = msg.FromNode,
            ToNode = msg.ToNode,
            TripDurationMinutes = msg.TripDurationMinutes,
            TotalPassengers = msg.TotalPassengers,
            TripsDone = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (bus is null)
        {
            job.Status = StatusValues.JobRejected;
            job.RejectReason = "No free bus available";
            job.TripsPlanned = 0;

            db.Jobs.Add(job);
            await db.SaveChangesAsync(ct);
            return;
        }

        var tripsPlanned = msg.TotalPassengers == 0
            ? 0
            : (int)Math.Ceiling(msg.TotalPassengers / (double)bus.Capacity);

        job.Status = tripsPlanned == 0 ? StatusValues.JobDone : StatusValues.JobRunning;
        job.BusId = bus.BusId;
        job.TripsPlanned = tripsPlanned;

        db.Jobs.Add(job);

        if (tripsPlanned > 0)
        {
            var passengers = BuildPassengerIds(msg);
            var createdTrips = new List<BusTripEntity>(tripsPlanned);
            for (var i = 0; i < tripsPlanned; i++)
            {
                var offset = i * bus.Capacity;
                var count = Math.Min(bus.Capacity, msg.TotalPassengers - offset);
                var segment = passengers.Skip(offset).Take(count).ToArray();
                var isFirst = i == 0;
                var tripId = Guid.NewGuid();

                var trip = new BusTripEntity
                {
                    TripId = tripId,
                    TaskId = msg.TaskId,
                    BusId = bus.BusId,
                    PlaneId = msg.PlaneId,
                    FlightId = msg.FlightId,
                    Status = isFirst ? StatusValues.TripStateMovingToPickup : StatusValues.TripStateCreated,
                    FromNode = msg.FromNode,
                    ToNode = msg.ToNode,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Passengers = segment.Select(x => new BusTripPassengerEntity
                    {
                        TripId = tripId,
                        PassengerId = x
                    }).ToList()
                };

                createdTrips.Add(trip);
            }

            db.Trips.AddRange(createdTrips);

            bus.State = StatusValues.BusStateMoving;
            bus.LocationNode = msg.FromNode;
            bus.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string[] BuildPassengerIds(HandlingTaskCreatedMessage msg)
    {
        if (msg.TotalPassengers == 0)
        {
            return [];
        }

        if (msg.PassengerIds.Count >= msg.TotalPassengers)
        {
            return msg.PassengerIds.Take(msg.TotalPassengers).ToArray();
        }

        var result = new List<string>(msg.TotalPassengers);
        result.AddRange(msg.PassengerIds);

        for (var i = result.Count; i < msg.TotalPassengers; i++)
        {
            result.Add($"PAX-{msg.TaskId}-{i + 1}");
        }

        return result.ToArray();
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int? GetInt(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var result = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var parsed = item.GetString();
                    if (!string.IsNullOrWhiteSpace(parsed))
                    {
                        result.Add(parsed);
                    }
                }
            }

            return result;
        }

        return [];
    }
}

