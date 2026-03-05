namespace Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "handling.task.created";
    public string GroupId { get; set; } = "bus-service";
}

