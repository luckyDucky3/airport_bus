using Confluent.Kafka;
using Infrastructure.Kafka;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Workers;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<OutboxPublisherWorker> logger
) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BusDbContext>();

                var events = await db.OutboxEvents
                    .Where(x => x.PublishedAt == null)
                    .OrderBy(x => x.CreatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                if (events.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                foreach (var item in events)
                {
                    try
                    {
                        await producer.ProduceAsync(item.Topic, new Message<string, string>
                        {
                            Key = item.EventKey,
                            Value = item.PayloadJson
                        }, stoppingToken);

                        item.PublishedAt = DateTimeOffset.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        item.PublishAttempts += 1;
                        logger.LogWarning(ex, "Failed to publish outbox event {EventId}", item.EventId);
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher worker failed");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        producer.Flush(TimeSpan.FromSeconds(5));
    }
}
