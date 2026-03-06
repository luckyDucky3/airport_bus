using System.ComponentModel.DataAnnotations;

namespace Models.Domain;

public class BusRuntimeEntity
{
    [Key]
    [MaxLength(32)]
    public string RuntimeId { get; set; } = "global";

    public DateTimeOffset? LastSimTime { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
