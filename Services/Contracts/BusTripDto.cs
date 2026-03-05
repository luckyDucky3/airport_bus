namespace Services.Contracts;

public sealed record BusTripDto(
    Guid TripId,
    string TaskId,
    string BusId,
    string PlaneId,
    string FlightId,
    string Status,
    string FromNode,
    string ToNode,
    Guid? RouteId,
    IReadOnlyList<string> PassengerIds,
    int PassengerCount,
    int DurationMinutes,
    int RemainingMinutes,
    DateTimeOffset? StartSimTime,
    DateTimeOffset? FinishSimTime,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

