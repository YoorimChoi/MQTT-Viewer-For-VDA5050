using System.Collections.ObjectModel;
using System.Text.Json;
using MqttViewer.Infrastructure;

namespace MqttViewer.Models;

public sealed class JsonTreeNode : ObservableObject
{
    private bool _hasChildren;
    private bool _isExpanded;
    private bool _isMatch;
    private bool _childrenLoaded = true;
    private Action<JsonTreeNode>? _loadChildrenAction;

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string ValueKind { get; set; } = "Unknown";

    public JsonElement SourceElement { get; private set; }

    public bool HasChildren
    {
        get => _hasChildren;
        private set => SetProperty(ref _hasChildren, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value))
            {
                return;
            }

            if (value && !_childrenLoaded)
            {
                _loadChildrenAction?.Invoke(this);
            }
        }
    }

    public bool IsMatch
    {
        get => _isMatch;
        set => SetProperty(ref _isMatch, value);
    }

    public ObservableCollection<JsonTreeNode> Children { get; } = new();

    public void ConfigureSource(JsonElement sourceElement, bool lazyLoadChildren, Action<JsonTreeNode>? loadChildrenAction = null)
    {
        SourceElement = sourceElement.Clone();
        _loadChildrenAction = loadChildrenAction;
        _childrenLoaded = !lazyLoadChildren;
        HasChildren = SourceElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
    }

    public void MarkChildrenLoaded()
    {
        _childrenLoaded = true;
    }
}
