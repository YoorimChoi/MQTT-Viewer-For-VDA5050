using MqttViewer.Infrastructure;

namespace MqttViewer.Models;

public sealed class TopicSummary : ObservableObject
{
    private string _topicName = string.Empty;
    private long _messageCount;
    private DateTime? _lastReceivedAt;
    private bool _isHighlighted;
    private string? _vdaGroupName;
    private string? _vehicleKey;

    public string TopicName
    {
        get => _topicName;
        set => SetProperty(ref _topicName, value);
    }

    public long MessageCount
    {
        get => _messageCount;
        set => SetProperty(ref _messageCount, value);
    }

    public DateTime? LastReceivedAt
    {
        get => _lastReceivedAt;
        set => SetProperty(ref _lastReceivedAt, value);
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    public string? VdaGroupName
    {
        get => _vdaGroupName;
        set => SetProperty(ref _vdaGroupName, value);
    }

    public string? VehicleKey
    {
        get => _vehicleKey;
        set => SetProperty(ref _vehicleKey, value);
    }
}
