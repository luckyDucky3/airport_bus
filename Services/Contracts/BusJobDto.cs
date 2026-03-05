namespace Services.Contracts;

public sealed record BusJobDto(
    string TaskId,
    string? HandlingId,
    string PlaneId,
    string FlightId,
    string Status,
    string? BusId,
    string FromNode,
    string ToNode,
    int TripDurationMinutes,
    int TripsPlanned,
    int TripsDone,
    int TotalPassengers,
    string? RejectReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

