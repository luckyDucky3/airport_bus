using System.ComponentModel.DataAnnotations;

namespace Models.Domain;

public class BusJobEntity
{
    [Key]
    [MaxLength(256)]
    public string TaskId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? HandlingId { get; set; }

    [MaxLength(128)]
    public string PlaneId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string FlightId { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Status { get; set; } = StatusValues.JobQueued;

    [MaxLength(128)]
    public string? BusId { get; set; }

    [MaxLength(128)]
    public string FromNode { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ToNode { get; set; } = string.Empty;

    public int TripDurationMinutes { get; set; }

    public int TripsPlanned { get; set; }

    public int TripsDone { get; set; }

    public int TotalPassengers { get; set; }

    [MaxLength(512)]
    public string? RejectReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<BusTripEntity> Trips { get; set; } = [];
}

