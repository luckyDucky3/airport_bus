namespace Infrastructure.Kafka;

public sealed class HandlingTaskCreatedMessage
{
    public string TaskId { get; init; } = string.Empty;
    public string? HandlingId { get; init; }
    public string PlaneId { get; init; } = string.Empty;
    public string FlightId { get; init; } = string.Empty;
    public string FromNode { get; init; } = string.Empty;
    public string ToNode { get; init; } = string.Empty;
    public int TripDurationMinutes { get; init; }
    public int TotalPassengers { get; init; }
    public IReadOnlyList<string> PassengerIds { get; init; } = [];
}

