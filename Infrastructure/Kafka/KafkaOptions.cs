namespace Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string HandlingTopic { get; set; } = "handling.events";
    public string SimTopic { get; set; } = "sim.events";
    public string BoardTopic { get; set; } = "board.events";
    public string GroupId { get; set; } = "bus-service";
}

