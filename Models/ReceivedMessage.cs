namespace MqttViewer.Models;

public sealed class ReceivedMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string TopicName { get; init; } = string.Empty;

    public DateTime ReceivedAt { get; init; }

    public int Qos { get; init; }

    public bool Retain { get; init; }

    public int PayloadSize { get; init; }

    public string PayloadRaw { get; init; } = string.Empty;

    public string? PayloadPrettyJson { get; init; }

    public bool IsJson { get; init; }

    public string Preview
    {
        get
        {
            var singleLine = PayloadRaw.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (singleLine.Length <= 100)
            {
                return singleLine;
            }

            return $"{singleLine[..100]}...";
        }
    }
}
