using MqttViewer.Infrastructure;
using MqttViewer.Services;

namespace MqttViewer.Models;

public sealed class SelectionOption : ObservableObject
{
    private readonly AppLocalizer _localizer = AppLocalizer.Instance;

    public SelectionOption(string value, string displayKey)
    {
        Value = value;
        DisplayKey = displayKey;
        _localizer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Item[]")
            {
                OnPropertyChanged(nameof(Display));
            }
        };
    }

    public string Value { get; }

    public string DisplayKey { get; }

    public string Display => _localizer[DisplayKey];
}
