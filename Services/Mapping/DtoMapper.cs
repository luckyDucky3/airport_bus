using Services.Contracts;
using Models.Domain;

namespace Services.Mapping;

public static class DtoMapper
{
    public static BusVehicleDto ToDto(this BusVehicleEntity entity) =>
        ToDto(entity, null);

    public static BusVehicleDto ToDto(this BusVehicleEntity entity, Guid? currentTripId) =>
        new(
            entity.BusId,
            entity.Capacity,
            MapBusStateToApiStatus(entity.State),
            entity.LocationNode,
            currentTripId,
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
        ToDto(entity, entity.Passengers.Select(p => p.PassengerId).ToArray());

    public static BusTripDto ToDto(this BusTripEntity entity, IReadOnlyList<string> passengerIds)
    {
        var apiStatus = MapTripStateToApiStatus(entity.Status);
        var duration = entity.Task?.TripDurationMinutes ?? 1;
        var isDone = apiStatus == StatusValues.ApiTripDone;
        var remaining = entity.Runtime?.RemainingMinutes ?? (isDone ? 0 : duration);
        var startSimTime = entity.Runtime?.StartSimTime ?? (apiStatus == StatusValues.ApiTripQueued ? null : entity.CreatedAt);
        var finishSimTime = entity.Runtime?.FinishSimTime ?? entity.DoneAt;

        return new(
            entity.TripId,
            entity.TaskId,
            entity.BusId,
            entity.PlaneId,
            entity.FlightId,
            apiStatus,
            entity.FromNode,
            entity.ToNode,
            entity.RouteId,
            passengerIds,
            passengerIds.Count,
            duration,
            remaining,
            startSimTime,
            finishSimTime,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }

    public static string MapBusStateToApiStatus(string state) =>
        state switch
        {
            StatusValues.BusStateFree => StatusValues.ApiVehicleFree,
            StatusValues.BusStateLoading => StatusValues.ApiVehicleBusy,
            StatusValues.BusStateMoving => StatusValues.ApiVehicleBusy,
            _ => StatusValues.ApiVehicleOffline
        };

    public static string MapTripStateToApiStatus(string state) =>
        state switch
        {
            StatusValues.TripStateCreated => StatusValues.ApiTripQueued,
            StatusValues.TripStateMovingToPickup => StatusValues.ApiTripRunning,
            StatusValues.TripStateLoading => StatusValues.ApiTripRunning,
            StatusValues.TripStateMovingToPlane => StatusValues.ApiTripRunning,
            StatusValues.TripStateDone => StatusValues.ApiTripDone,
            _ => StatusValues.ApiTripFailed
        };

    public static IReadOnlyCollection<string> MapApiTripStatusToStates(string apiStatus) =>
        apiStatus switch
        {
            StatusValues.ApiTripQueued => [StatusValues.TripStateCreated],
            StatusValues.ApiTripRunning => [StatusValues.TripStateMovingToPickup, StatusValues.TripStateLoading, StatusValues.TripStateMovingToPlane],
            StatusValues.ApiTripDone => [StatusValues.TripStateDone],
            StatusValues.ApiTripFailed => [],
            _ => []
        };

    public static IReadOnlyCollection<string> MapApiVehicleStatusToStates(string apiStatus) =>
        apiStatus switch
        {
            StatusValues.ApiVehicleFree => [StatusValues.BusStateFree],
            StatusValues.ApiVehicleBusy => [StatusValues.BusStateLoading, StatusValues.BusStateMoving],
            StatusValues.ApiVehicleOffline => [],
            _ => []
        };

    public static ErrorResponse ValidationError(string message) =>
        new("validation_error", message);

    public static ErrorResponse NotFound(string message) =>
        new("not_found", message);

    public static ErrorResponse InternalError(string message) =>
        new("internal_error", message);
}


