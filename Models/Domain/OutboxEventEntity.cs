using System.ComponentModel.DataAnnotations;

namespace Models.Domain;

public class OutboxEventEntity
{
    [Key]
    public Guid EventId { get; set; }

    [MaxLength(128)]
    public string Topic { get; set; } = string.Empty;

    [MaxLength(128)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(256)]
    public string EventKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int PublishAttempts { get; set; }
}
