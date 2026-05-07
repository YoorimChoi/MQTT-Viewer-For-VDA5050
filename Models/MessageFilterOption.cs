using MqttViewer.Infrastructure;

namespace MqttViewer.Models;

public sealed class MessageFilterOption : ObservableObject
{
    private bool _isSelected;

    public MessageFilterOption(string value, int count)
    {
        Value = value;
        Count = count;
    }

    public string Value { get; }

    public int Count { get; }

    public string DisplayText => $"{Value} ({Count})";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
