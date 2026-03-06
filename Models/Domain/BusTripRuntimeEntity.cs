using System.ComponentModel.DataAnnotations;

namespace Models.Domain;

public class BusTripRuntimeEntity
{
    [Key]
    public Guid TripId { get; set; }

    public int RemainingMinutes { get; set; }

    public DateTimeOffset? StartSimTime { get; set; }

    public DateTimeOffset? FinishSimTime { get; set; }

    public BusTripEntity? Trip { get; set; }
}
