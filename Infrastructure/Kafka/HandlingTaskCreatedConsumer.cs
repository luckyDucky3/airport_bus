using System.Text.Json;
using Confluent.Kafka;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Domain;

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
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(_options.HandlingTopic);

        logger.LogInformation("Handling consumer started. Topic={Topic}, GroupId={GroupId}", _options.HandlingTopic, _options.GroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                {
                    continue;
                }

                if (!KafkaEventParser.TryParseHandlingTaskCreated(result.Message.Value, out var eventId, out var payload))
                {
                    consumer.Commit(result);
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BusDbContext>();

                await ProcessAsync(db, eventId, payload, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error in handling consumer");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in handling consumer");
            }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(BusDbContext db, Guid eventId, Contracts.HandlingTaskCreatedPayload payload, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var processed = await TryInsertProcessedEventAsync(db, eventId, ct);
        if (!processed)
        {
            await tx.CommitAsync(ct);
            return;
        }

        var existing = await db.Jobs.AsNoTracking().AnyAsync(x => x.TaskId == payload.TaskId, ct);
        if (existing)
        {
            await tx.CommitAsync(ct);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var isValid = IsValidPayload(payload);
        if (!isValid)
        {
            var rejected = new BusJobEntity
            {
                TaskId = payload.TaskId,
                HandlingId = payload.HandlingId,
                PlaneId = payload.PlaneId,
                FlightId = payload.FlightId,
                FromNode = payload.Payload?.FromNode ?? string.Empty,
                ToNode = payload.Payload?.ToNode ?? string.Empty,
                TripDurationMinutes = Math.Max(payload.Payload?.TripDurationMinutes ?? 0, 0),
                Status = StatusValues.JobRejected,
                RejectReason = "Invalid task payload",
                TripsPlanned = 0,
                TripsDone = 0,
                TotalPassengers = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Jobs.Add(rejected);
            db.OutboxEvents.Add(CreateTaskRejectedOutbox(_options.HandlingTopic, payload, now));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return;
        }

        var preferredBusId = payload.Payload?.BusId;
        BusVehicleEntity? bus;
        if (!string.IsNullOrWhiteSpace(preferredBusId))
        {
            bus = await db.Buses.FirstOrDefaultAsync(x => x.BusId == preferredBusId, ct);
        }
        else
        {
            bus = await db.Buses
                .Where(x => x.State == StatusValues.BusStateFree)
                .OrderBy(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }

        var job = new BusJobEntity
        {
            TaskId = payload.TaskId,
            HandlingId = payload.HandlingId,
            PlaneId = payload.PlaneId,
            FlightId = payload.FlightId,
            FromNode = payload.Payload!.FromNode,
            ToNode = payload.Payload!.ToNode,
            TripDurationMinutes = payload.Payload!.TripDurationMinutes,
            Status = StatusValues.JobQueued,
            BusId = bus?.BusId,
            TripsPlanned = 0,
            TripsDone = 0,
            TotalPassengers = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Runtime = new BusJobRuntimeEntity
            {
                TaskId = payload.TaskId,
                PickupCompleted = false,
                UpdatedAt = now
            }
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static bool IsValidPayload(Contracts.HandlingTaskCreatedPayload payload)
    {
        return !string.IsNullOrWhiteSpace(payload.TaskId)
               && string.Equals(payload.TaskType, "bus", StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(payload.PlaneId)
               && !string.IsNullOrWhiteSpace(payload.FlightId)
               && payload.Payload is not null
               && !string.IsNullOrWhiteSpace(payload.Payload.FromNode)
               && !string.IsNullOrWhiteSpace(payload.Payload.ToNode)
               && payload.Payload.TripDurationMinutes > 0;
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

    private static OutboxEventEntity CreateTaskRejectedOutbox(string topic, Contracts.HandlingTaskCreatedPayload payload, DateTimeOffset now)
    {
        var body = new
        {
            eventId = Guid.NewGuid(),
            type = "handling.task.rejected",
            taskType = "bus",
            payload = new
            {
                taskId = payload.TaskId,
                taskType = "bus",
                planeId = payload.PlaneId,
                flightId = payload.FlightId,
                reason = "invalid_payload"
            }
        };

        return new OutboxEventEntity
        {
            EventId = Guid.NewGuid(),
            Topic = topic,
            EventType = "handling.task.rejected",
            EventKey = payload.TaskId,
            PayloadJson = JsonSerializer.Serialize(body),
            CreatedAt = now,
            PublishAttempts = 0
        };
    }
}
