namespace MqttViewer.Models;

public sealed class DiffLine
{
    public string Kind { get; init; } = "Context";

    public string Text { get; init; } = string.Empty;
}
