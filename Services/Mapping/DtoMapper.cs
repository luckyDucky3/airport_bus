using Services.Contracts;
using Models.Domain;

namespace Services.Mapping;

public static class DtoMapper
{
    public static BusVehicleDto ToDto(this BusVehicleEntity entity) =>
        new(
            entity.BusId,
            entity.Capacity,
            entity.Status,
            entity.LocationNode,
            entity.CurrentTripId,
            entity.RouteId,
            entity.UpdatedAt
        );

    public static BusJobDto ToDto(this BusJobEntity entity) =>
        new(
            entity.TaskId,
            entity.HandlingId,
            entity.PlaneId,
            entity.FlightId,
            entity.Status,
            entity.BusId,
            entity.FromNode,
            entity.ToNode,
            entity.TripDurationMinutes,
            entity.TripsPlanned,
            entity.TripsDone,
            entity.TotalPassengers,
            entity.RejectReason,
            entity.CreatedAt,
            entity.UpdatedAt
        );

    public static BusTripDto ToDto(this BusTripEntity entity) =>
        new(
            entity.TripId,
            entity.TaskId,
            entity.BusId,
            entity.PlaneId,
            entity.FlightId,
            entity.Status,
            entity.FromNode,
            entity.ToNode,
            entity.RouteId,
            entity.PassengerIds,
            entity.PassengerCount,
            entity.DurationMinutes,
            entity.RemainingMinutes,
            entity.StartSimTime,
            entity.FinishSimTime,
            entity.CreatedAt,
            entity.UpdatedAt
        );

    public static ErrorResponse ValidationError(string message) =>
        new("validation_error", message);

    public static ErrorResponse NotFound(string message) =>
        new("not_found", message);

    public static ErrorResponse InternalError(string message) =>
        new("internal_error", message);
}


