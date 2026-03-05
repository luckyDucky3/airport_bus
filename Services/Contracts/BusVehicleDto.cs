namespace Services.Contracts;

public sealed record BusVehicleDto(
    string BusId,
    int Capacity,
    string Status,
    string LocationNode,
    Guid? CurrentTripId,
    Guid? RouteId,
    DateTimeOffset UpdatedAt
);

