using System.ComponentModel.DataAnnotations;

namespace Models.Domain;

public class ProcessedEventEntity
{
    [Key]
    public Guid EventId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
