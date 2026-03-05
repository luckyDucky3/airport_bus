namespace Models.Domain;

public class BusTripPassengerEntity
{
    public Guid TripId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string PassengerId { get; set; } = string.Empty;

    public BusTripEntity? Trip { get; set; }
}
