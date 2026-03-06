namespace Models.Domain;

public class BusJobRuntimeEntity
{
    public string TaskId { get; set; } = string.Empty;

    public bool PickupCompleted { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public BusJobEntity? Job { get; set; }
}
