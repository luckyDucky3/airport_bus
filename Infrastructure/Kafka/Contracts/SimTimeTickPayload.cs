namespace Infrastructure.Kafka.Contracts;

public sealed class SimTimeTickPayload
{
    public DateTimeOffset SimTime { get; init; }
    public int TickMinutes { get; init; }
    public bool Paused { get; init; }
}
