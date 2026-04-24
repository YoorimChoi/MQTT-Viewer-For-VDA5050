using System.Collections.ObjectModel;

namespace MqttViewer.Models;

public sealed class JsonTreeNode
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string ValueKind { get; set; } = "Unknown";

    public bool IsExpanded { get; set; }

    public bool IsMatch { get; set; }

    public ObservableCollection<JsonTreeNode> Children { get; } = new();
}
