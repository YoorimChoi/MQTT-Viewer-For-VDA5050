namespace MqttViewer.Services;

public sealed class IncomingMqttMessage
{
    public required string Topic { get; init; }

    public required string Payload { get; init; }

    public int Qos { get; init; }

    public bool Retain { get; init; }

    public DateTime ReceivedAt { get; init; }
}
