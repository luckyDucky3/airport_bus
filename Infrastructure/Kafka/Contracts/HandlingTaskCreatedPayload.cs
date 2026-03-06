namespace Infrastructure.Kafka.Contracts;

public sealed class HandlingTaskCreatedPayload
{
    public string TaskId { get; init; } = string.Empty;
    public string? HandlingId { get; init; }
    public string TaskType { get; init; } = string.Empty;
    public string PlaneId { get; init; } = string.Empty;
    public string FlightId { get; init; } = string.Empty;
    public HandlingTaskBusPayload? Payload { get; init; }
}

public sealed class HandlingTaskBusPayload
{
    public string FromNode { get; init; } = string.Empty;
    public string ToNode { get; init; } = string.Empty;
    public int TripDurationMinutes { get; init; }
    public string? BusId { get; init; }
}
