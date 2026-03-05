using System.ComponentModel.DataAnnotations;

namespace Models.Domain;

public class BusTripEntity
{
    [Key]
    public Guid TripId { get; set; }

    [MaxLength(256)]
    public string TaskId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string BusId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string PlaneId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string FlightId { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Status { get; set; } = StatusValues.TripQueued;

    [MaxLength(128)]
    public string FromNode { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ToNode { get; set; } = string.Empty;

    public Guid? RouteId { get; set; }

    public string[] PassengerIds { get; set; } = [];

    public int PassengerCount { get; set; }

    public int DurationMinutes { get; set; }

    public int RemainingMinutes { get; set; }

    public DateTimeOffset? StartSimTime { get; set; }

    public DateTimeOffset? FinishSimTime { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public BusJobEntity? Task { get; set; }
}

