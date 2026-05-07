namespace MqttViewer.Models;

public sealed class ReceivedMessage
{
    private static readonly string[] KnownVdaGroups = ["order", "state", "instantActions", "connection", "visualization", "factsheet", "zoneSet", "responses"];

    public Guid Id { get; init; } = Guid.NewGuid();

    public string TopicName { get; init; } = string.Empty;

    public DateTime ReceivedAt { get; init; }

    public int Qos { get; init; }

    public bool Retain { get; init; }

    public int PayloadSize { get; init; }

    public string PayloadRaw { get; init; } = string.Empty;

    public string? PayloadPrettyJson { get; init; }

    public bool IsJson { get; init; }

    public string VehicleKey => GuessVehicleKey(TopicName);

    public string MessageType => GuessMessageType(TopicName);

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

    private static string GuessVehicleKey(string topic)
    {
        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return "unknown";
        }

        var groupIndex = Array.FindIndex(
            segments,
            segment => KnownVdaGroups.Contains(segment, StringComparer.OrdinalIgnoreCase));

        if (groupIndex >= 2)
        {
            return $"{segments[groupIndex - 2]}/{segments[groupIndex - 1]}";
        }

        if (segments.Length >= 5 && string.Equals(segments[0], "uagv", StringComparison.OrdinalIgnoreCase))
        {
            return $"{segments[2]}/{segments[3]}";
        }

        return segments.Length >= 2 ? $"{segments[^2]}/{segments[^1]}" : "unknown";
    }

    private static string GuessMessageType(string topic)
    {
        foreach (var group in KnownVdaGroups)
        {
            if (topic.Contains($"/{group}", StringComparison.OrdinalIgnoreCase) ||
                topic.EndsWith(group, StringComparison.OrdinalIgnoreCase))
            {
                return group;
            }
        }

        return "other";
    }
}
