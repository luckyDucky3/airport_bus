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
    public string Status { get; set; } = StatusValues.TripStateCreated;

    [MaxLength(128)]
    public string FromNode { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ToNode { get; set; } = string.Empty;

    public Guid? RouteId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DoneAt { get; set; }

    public BusJobEntity? Task { get; set; }

    public List<BusTripPassengerEntity> Passengers { get; set; } = [];

    public BusTripRuntimeEntity? Runtime { get; set; }
}

