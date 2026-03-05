namespace Models.Domain;

public static class StatusValues
{
    public const string VehicleFree = "free";
    public const string VehicleBusy = "busy";
    public const string VehicleOffline = "offline";

    public const string JobQueued = "queued";
    public const string JobRunning = "running";
    public const string JobDone = "done";
    public const string JobRejected = "rejected";

    public const string TripQueued = "queued";
    public const string TripRunning = "running";
    public const string TripDone = "done";
    public const string TripFailed = "failed";

    public static readonly HashSet<string> VehicleStatuses =
    [
        VehicleFree,
        VehicleBusy,
        VehicleOffline
    ];

    public static readonly HashSet<string> JobStatuses =
    [
        JobQueued,
        JobRunning,
        JobDone,
        JobRejected
    ];

    public static readonly HashSet<string> TripStatuses =
    [
        TripQueued,
        TripRunning,
        TripDone,
        TripFailed
    ];
}

