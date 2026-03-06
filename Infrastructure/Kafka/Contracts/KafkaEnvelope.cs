namespace Infrastructure.Kafka.Contracts;

public sealed class KafkaEnvelope<TPayload>
{
    public Guid EventId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? TaskType { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
    public TPayload? Payload { get; init; }
}
