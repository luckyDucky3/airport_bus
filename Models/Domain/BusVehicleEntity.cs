using System.ComponentModel.DataAnnotations;

namespace Models.Domain;

public class BusVehicleEntity
{
    [Key]
    [MaxLength(128)]
    public string BusId { get; set; } = string.Empty;

    public int Capacity { get; set; }

    [MaxLength(16)]
    public string Status { get; set; } = StatusValues.VehicleFree;

    [MaxLength(128)]
    public string LocationNode { get; set; } = string.Empty;

    public Guid? CurrentTripId { get; set; }

    public Guid? RouteId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

