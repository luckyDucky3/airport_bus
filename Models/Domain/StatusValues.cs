namespace Models.Domain;

public static class StatusValues
{
    public const string BusStateFree = "free";
    public const string BusStateLoading = "loading";
    public const string BusStateMoving = "moving";

    public const string ApiVehicleFree = "free";
    public const string ApiVehicleBusy = "busy";
    public const string ApiVehicleOffline = "offline";

    public const string JobQueued = "queued";
    public const string JobRunning = "running";
    public const string JobDone = "done";
    public const string JobRejected = "rejected";

    public const string TripStateCreated = "created";
    public const string TripStateMovingToPickup = "moving_to_pickup";
    public const string TripStateLoading = "loading";
    public const string TripStateMovingToPlane = "moving_to_plane";
    public const string TripStateDone = "done";

    public const string ApiTripQueued = "queued";
    public const string ApiTripRunning = "running";
    public const string ApiTripDone = "done";
    public const string ApiTripFailed = "failed";

    public static readonly HashSet<string> BusStates =
    [
        BusStateFree,
        BusStateLoading,
        BusStateMoving
    ];

    public static readonly HashSet<string> JobStatuses =
    [
        JobQueued,
        JobRunning,
        JobDone,
        JobRejected
    ];

    public static readonly HashSet<string> TripStates =
    [
        TripStateCreated,
        TripStateMovingToPickup,
        TripStateLoading,
        TripStateMovingToPlane,
        TripStateDone
    ];

    public static readonly HashSet<string> ApiTripStatuses =
    [
        ApiTripQueued,
        ApiTripRunning,
        ApiTripDone,
        ApiTripFailed
    ];

    public static readonly HashSet<string> ApiVehicleStatuses =
    [
        ApiVehicleFree,
        ApiVehicleBusy,
        ApiVehicleOffline
    ];
}

