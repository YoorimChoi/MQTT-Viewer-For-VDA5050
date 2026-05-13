using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using MqttViewer.Infrastructure;
using MqttViewer.Models;
using MqttViewer.Services;

namespace MqttViewer.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly string[] KnownVdaGroups = ["order", "state", "instantActions", "connection", "visualization", "factsheet", "zoneSet", "responses"];

    private readonly IMqttMonitorService _mqttMonitorService;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, List<ReceivedMessage>> _messagesByTopic = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TopicSummary> _topicByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> _highlightByTopic = new(StringComparer.Ordinal);
    private readonly List<JsonTreeNode> _rawJsonTreeNodes = [];
    private readonly HashSet<string> _selectedVehicleFilters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedMessageTypeFilters = new(StringComparer.Ordinal);

    private TopicSummary? _selectedTopic;
    private ReceivedMessage? _selectedMessage;
    private string _topicSearchText = string.Empty;
    private string _jsonSearchText = string.Empty;
    private string _selectedTopicSortMode = "TopicNameAsc";
    private string _selectedTopicGroupMode = "Vehicle";
    private string _selectedMessageSortMode = "NewestFirst";
    private string _selectedExportScope = "AllTopics";
    private string _allMessagesFilterText = string.Empty;
    private bool _isVehicleFilterPopupOpen;
    private bool _isMessageTypeFilterPopupOpen;
    private bool _isPublishDialogOpen;
    private bool _isSelectionMode;
    private string _publishTopic = string.Empty;
    private string _publishPayload = string.Empty;
    private string _selectedPublishQos = "0";
    private bool _publishRetain;
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
    private string _statusText = string.Empty;
    private string _sessionSourceText = string.Empty;
    private string _selectedMessageDiffSummary = string.Empty;
    private bool _isImportedSession;
    private long _totalReceivedCount;
    private DateTime? _lastReceivedAt;
    private string _selectedLanguageCode;

    private AppLocalizer LocalizerCore => AppLocalizer.Instance;

    public MainWindowViewModel(IMqttMonitorService mqttMonitorService)
    {
        _mqttMonitorService = mqttMonitorService;
        _dispatcher = Application.Current.Dispatcher;
        _selectedLanguageCode = LocalizerCore.CurrentLanguageCode;

        ConnectionSettings = new ConnectionSettings();
        Localizer = LocalizerCore;

        Topics = new ObservableCollection<TopicSummary>();
        TopicsView = CollectionViewSource.GetDefaultView(Topics);
        TopicsView.Filter = TopicFilterPredicate;
        ApplyTopicGroupingAndSorting();

        CurrentTopicMessages = new ObservableCollection<ReceivedMessage>();
        MessagesView = CollectionViewSource.GetDefaultView(CurrentTopicMessages);
        ApplyMessageSorting();

        AllMessages = new ObservableCollection<ReceivedMessage>();
        AllMessagesView = CollectionViewSource.GetDefaultView(AllMessages);
        AllMessagesView.Filter = AllMessagesFilterPredicate;
        ApplyAllMessagesSorting();

        VehicleFilterOptions = new ObservableCollection<MessageFilterOption>();
        MessageTypeFilterOptions = new ObservableCollection<MessageFilterOption>();

        LanguageOptions = new ObservableCollection<SelectionOption>
        {
            new("en", "LanguageEnglish"),
            new("ko", "LanguageKorean")
        };
        TopicSortModes = new ObservableCollection<SelectionOption>
        {
            new("RecentFirst", "SortRecentFirst"),
            new("TopicNameAsc", "SortTopicNameAsc")
        };
        TopicGroupModes = new ObservableCollection<SelectionOption>
        {
            new("None", "GroupNone"),
            new("Vda5050Group", "GroupVda5050"),
            new("Vehicle", "GroupVehicle")
        };
        MessageSortModes = new ObservableCollection<SelectionOption>
        {
            new("NewestFirst", "SortNewestFirst"),
            new("OldestFirst", "SortOldestFirst")
        };
        ExportScopes = new ObservableCollection<SelectionOption>
        {
            new("SelectedMessage", "ExportSelectedMessage"),
            new("SelectedMessages", "ExportSelectedMessages"),
            new("SelectedTopic", "ExportSelectedTopic"),
            new("FilteredAllMessages", "ExportFilteredAllMessages"),
            new("AllTopics", "ExportAllTopics")
        };
        PublishQosOptions = new ObservableCollection<SelectionOption>
        {
            new("0", "PublishQos0"),
            new("1", "PublishQos1"),
            new("2", "PublishQos2")
        };
        JsonTreeNodes = new ObservableCollection<JsonTreeNode>();
        SelectedMessageDiffLines = new ObservableCollection<DiffLine>();

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
        SwitchToImportedSessionCommand = new AsyncRelayCommand(SwitchToImportedSessionAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        StartLiveSessionCommand = new RelayCommand(StartLiveSession);
        ClearAllCommand = new RelayCommand(ClearAllMessages, () => TotalReceivedCount > 0);
        ClearSelectedTopicCommand = new RelayCommand(ClearSelectedTopicMessages, () => SelectedTopic is not null);
        ClearSelectedMessageCommand = new RelayCommand(ClearSelectedMessage, () => SelectedMessage is not null);
        ClearSelectedMessagesCommand = new RelayCommand(ClearSelectedMessages, HasSelectedMessages);
        ClearMessageSelectionCommand = new RelayCommand(ClearMessageSelection, HasSelectedMessages);
        ClearAllMessagesFilterCommand = new RelayCommand(ClearAllMessagesFilter, HasAnyAllMessagesFilter);
        ToggleVehicleFilterPopupCommand = new RelayCommand(() => IsVehicleFilterPopupOpen = !IsVehicleFilterPopupOpen);
        ToggleMessageTypeFilterPopupCommand = new RelayCommand(() => IsMessageTypeFilterPopupOpen = !IsMessageTypeFilterPopupOpen);
        OpenPublishDialogCommand = new RelayCommand(OpenPublishDialog, CanOpenPublishDialog);
        ClosePublishDialogCommand = new RelayCommand(ClosePublishDialog);
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanExport);
        PublishCommand = new AsyncRelayCommand(PublishAsync, CanPublish);
        CopyRawCommand = new RelayCommand(CopyRawPayload, () => SelectedMessage is not null);
        CopyPrettyCommand = new RelayCommand(CopyPrettyPayload, () => SelectedMessage is not null);
        CopyFullCommand = new RelayCommand(CopyMessageWithMetadata, () => SelectedMessage is not null);
        CollapseJsonTreeToRootCommand = new RelayCommand(CollapseJsonTreeToRoot, CanManipulateJsonTree);
        ExpandJsonTreeCommand = new RelayCommand(ExpandJsonTree, CanManipulateJsonTree);

        _mqttMonitorService.ConnectionStatusChanged += OnConnectionStatusChanged;
        _mqttMonitorService.ConnectionErrorOccurred += OnConnectionErrorOccurred;
        _mqttMonitorService.MessageReceived += OnMessageReceived;
        Localizer.PropertyChanged += OnLocalizerPropertyChanged;

        SessionSourceText = T("NoBrokerConnected");
        StatusText = T("StatusDisconnected");
        SelectedMessageDiffSummary = T("StatusNoMessageSelected");
    }

    public AppLocalizer Localizer { get; }

    public ConnectionSettings ConnectionSettings { get; }

    public ObservableCollection<TopicSummary> Topics { get; }

    public ICollectionView TopicsView { get; }

    public ObservableCollection<ReceivedMessage> CurrentTopicMessages { get; }

    public ICollectionView MessagesView { get; }

    public ObservableCollection<ReceivedMessage> AllMessages { get; }

    public ICollectionView AllMessagesView { get; }

    public ObservableCollection<MessageFilterOption> VehicleFilterOptions { get; }

    public ObservableCollection<MessageFilterOption> MessageTypeFilterOptions { get; }

    public ObservableCollection<SelectionOption> LanguageOptions { get; }

    public ObservableCollection<SelectionOption> TopicSortModes { get; }

    public ObservableCollection<SelectionOption> TopicGroupModes { get; }

    public ObservableCollection<SelectionOption> MessageSortModes { get; }

    public ObservableCollection<SelectionOption> ExportScopes { get; }

    public ObservableCollection<SelectionOption> PublishQosOptions { get; }

    public ObservableCollection<JsonTreeNode> JsonTreeNodes { get; }

    public ObservableCollection<DiffLine> SelectedMessageDiffLines { get; }

    public TopicSummary? SelectedTopic
    {
        get => _selectedTopic;
        set
        {
            if (!SetProperty(ref _selectedTopic, value))
            {
                return;
            }

            RebuildCurrentTopicMessages();
            OnPropertyChanged(nameof(SelectedTopicMessageCount));
            OnPropertyChanged(nameof(SelectedTopicMessageCountText));
            OnPropertyChanged(nameof(SelectedTopicDisplayText));
            UpdateCommandState();
        }
    }

    public ReceivedMessage? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            if (!SetProperty(ref _selectedMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedMessageRawText));
            OnPropertyChanged(nameof(SelectedMessagePrettyText));
            OnPropertyChanged(nameof(SelectedMessageJsonStatus));
            RebuildJsonTree();
            RebuildSelectedMessageDiff();
            UpdateCommandState();
        }
    }

    public string TopicSearchText
    {
        get => _topicSearchText;
        set
        {
            if (!SetProperty(ref _topicSearchText, value))
            {
                return;
            }

            TopicsView.Refresh();
        }
    }

    public string JsonSearchText
    {
        get => _jsonSearchText;
        set
        {
            if (!SetProperty(ref _jsonSearchText, value))
            {
                return;
            }

            ApplyJsonTreeFilter();
        }
    }

    public string SelectedTopicSortMode
    {
        get => _selectedTopicSortMode;
        set
        {
            if (!SetProperty(ref _selectedTopicSortMode, value))
            {
                return;
            }

            ApplyTopicGroupingAndSorting();
        }
    }

    public string SelectedTopicGroupMode
    {
        get => _selectedTopicGroupMode;
        set
        {
            if (!SetProperty(ref _selectedTopicGroupMode, value))
            {
                return;
            }

            ApplyTopicGroupingAndSorting();
        }
    }

    public string SelectedMessageSortMode
    {
        get => _selectedMessageSortMode;
        set
        {
            if (!SetProperty(ref _selectedMessageSortMode, value))
            {
                return;
            }

            ApplyMessageSorting();
        }
    }

    public string SelectedExportScope
    {
        get => _selectedExportScope;
        set
        {
            if (!SetProperty(ref _selectedExportScope, value))
            {
                return;
            }

            ExportCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (!SetProperty(ref _isSelectionMode, value))
            {
                return;
            }

            if (!value)
            {
                ClearMessageSelection();
            }

            OnPropertyChanged(nameof(SelectionColumnWidth));
            UpdateCommandState();
        }
    }

    public bool IsPublishDialogOpen
    {
        get => _isPublishDialogOpen;
        set => SetProperty(ref _isPublishDialogOpen, value);
    }

    public string PublishTopic
    {
        get => _publishTopic;
        set
        {
            if (!SetProperty(ref _publishTopic, value))
            {
                return;
            }

            PublishCommand.RaiseCanExecuteChanged();
        }
    }

    public string PublishPayload
    {
        get => _publishPayload;
        set => SetProperty(ref _publishPayload, value);
    }

    public string SelectedPublishQos
    {
        get => _selectedPublishQos;
        set => SetProperty(ref _selectedPublishQos, value);
    }

    public bool PublishRetain
    {
        get => _publishRetain;
        set => SetProperty(ref _publishRetain, value);
    }

    public string AllMessagesFilterText
    {
        get => _allMessagesFilterText;
        set
        {
            if (!SetProperty(ref _allMessagesFilterText, value))
            {
                return;
            }

            RefreshAllMessagesViewState();
        }
    }

    public bool IsVehicleFilterPopupOpen
    {
        get => _isVehicleFilterPopupOpen;
        set => SetProperty(ref _isVehicleFilterPopupOpen, value);
    }

    public bool IsMessageTypeFilterPopupOpen
    {
        get => _isMessageTypeFilterPopupOpen;
        set => SetProperty(ref _isMessageTypeFilterPopupOpen, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set
        {
            if (!SetProperty(ref _selectedLanguageCode, value))
            {
                return;
            }

            Localizer.CurrentLanguageCode = value;
        }
    }

    public bool IsDarkMode
    {
        get => AppThemeManager.Instance.IsDarkMode;
        set
        {
            if (AppThemeManager.Instance.IsDarkMode == value)
            {
                return;
            }

            AppThemeManager.Instance.IsDarkMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeModeText));
        }
    }

    public string ThemeModeText => IsDarkMode ? Localizer["ThemeDark"] : Localizer["ThemeLight"];

    public string ConnectionStatusText => _connectionStatus switch
    {
        ConnectionStatus.Connected => T("ConnectionStatusConnected"),
        ConnectionStatus.Connecting => T("ConnectionStatusConnecting"),
        ConnectionStatus.Error => T("ConnectionStatusError"),
        _ => T("StatusDisconnected")
    };

    public bool IsConnected => _connectionStatus == ConnectionStatus.Connected;

    public bool IsImportedSession
    {
        get => _isImportedSession;
        private set
        {
            if (!SetProperty(ref _isImportedSession, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SessionModeText));
            OnPropertyChanged(nameof(SessionModeDescription));
            OnPropertyChanged(nameof(ModePanelTitleText));
            OnPropertyChanged(nameof(SessionSourceCaption));
            OnPropertyChanged(nameof(IsLiveSession));
            UpdateCommandState();
        }
    }

    public bool IsLiveSession => !IsImportedSession;

    public string SessionModeText => IsImportedSession ? Localizer["SessionImported"] : Localizer["SessionLive"];

    public string SessionModeDescription => IsImportedSession
        ? Localizer["SessionImportedDescription"]
        : Localizer["SessionLiveDescription"];

    public string ModePanelTitleText => IsImportedSession ? Localizer["ImportedFile"] : Localizer["Connection"];

    public string SessionSourceCaption => IsImportedSession ? Localizer["ImportedFile"] : Localizer["Connection"];

    public string SessionSourceText
    {
        get => _sessionSourceText;
        private set => SetProperty(ref _sessionSourceText, value);
    }

    public long TotalReceivedCount
    {
        get => _totalReceivedCount;
        private set
        {
            if (!SetProperty(ref _totalReceivedCount, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TotalReceivedCountText));
            OnPropertyChanged(nameof(AllMessagesResultCountText));
        }
    }

    public DateTime? LastReceivedAt
    {
        get => _lastReceivedAt;
        private set
        {
            if (!SetProperty(ref _lastReceivedAt, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LastReceivedAtText));
        }
    }

    public string LastReceivedAtText => LastReceivedAt is null
        ? $"{Localizer["LastReceivedLabel"]}: -"
        : $"{Localizer["LastReceivedLabel"]}: {LastReceivedAt:HH:mm:ss.fff}";

    public string TotalReceivedCountText => TF("TotalLabel", TotalReceivedCount);

    public string SelectedTopicMessageCountText => TF("SelectedTopicLabel", SelectedTopicMessageCount);

    public string AllMessagesResultCountText => TF("AllMessagesResultCount", AllMessagesFilteredCount, AllMessages.Count);

    public string AllMessagesFilteredCountText => TF("AllMessagesFilteredCount", AllMessagesFilteredCount);

    public int SelectedMessagesCount => AllMessages.Count(message => message.IsSelected);

    public string SelectedMessagesCountText => TF("CheckedMessagesLabel", SelectedMessagesCount);

    public DataGridLength SelectionColumnWidth => IsSelectionMode ? new DataGridLength(44) : new DataGridLength(0);

    public string VehicleFilterSummary => BuildFilterSummary(_selectedVehicleFilters);

    public string MessageTypeFilterSummary => BuildFilterSummary(_selectedMessageTypeFilters);

    public string SelectedMessageDiffSummary
    {
        get => _selectedMessageDiffSummary;
        private set => SetProperty(ref _selectedMessageDiffSummary, value);
    }

    public int SelectedTopicMessageCount => CurrentTopicMessages.Count;

    public int AllMessagesFilteredCount => AllMessagesView.Cast<object>().Count();

    public string SelectedTopicDisplayText => SelectedTopic?.TopicName ?? Localizer["NoTopicSelected"];

    public string SelectedMessageRawText => SelectedMessage?.PayloadRaw ?? string.Empty;

    public string SelectedMessagePrettyText
    {
        get
        {
            if (SelectedMessage is null)
            {
                return string.Empty;
            }

            return SelectedMessage.IsJson
                ? SelectedMessage.PayloadPrettyJson ?? SelectedMessage.PayloadRaw
                : SelectedMessage.PayloadRaw;
        }
    }

    public string SelectedMessageJsonStatus
    {
        get
        {
            if (SelectedMessage is null)
            {
                return T("StatusNoMessageSelected");
            }

            return SelectedMessage.IsJson ? T("StatusJsonParsed") : T("StatusInvalidJson");
        }
    }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand DisconnectCommand { get; }

    public AsyncRelayCommand SwitchToImportedSessionCommand { get; }

    public AsyncRelayCommand ImportCommand { get; }

    public RelayCommand StartLiveSessionCommand { get; }

    public RelayCommand ClearAllCommand { get; }

    public RelayCommand ClearSelectedTopicCommand { get; }

    public RelayCommand ClearSelectedMessageCommand { get; }

    public RelayCommand ClearSelectedMessagesCommand { get; }

    public RelayCommand ClearMessageSelectionCommand { get; }

    public RelayCommand ClearAllMessagesFilterCommand { get; }

    public RelayCommand ToggleVehicleFilterPopupCommand { get; }

    public RelayCommand ToggleMessageTypeFilterPopupCommand { get; }

    public RelayCommand OpenPublishDialogCommand { get; }

    public RelayCommand ClosePublishDialogCommand { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public AsyncRelayCommand PublishCommand { get; }

    public RelayCommand CopyRawCommand { get; }

    public RelayCommand CopyPrettyCommand { get; }

    public RelayCommand CopyFullCommand { get; }

    public RelayCommand CollapseJsonTreeToRootCommand { get; }

    public RelayCommand ExpandJsonTreeCommand { get; }

    private string T(string key) => Localizer[key];

    private string TF(string key, params object[] args) => Localizer.Format(key, args);

    private void OnLocalizerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]")
        {
            return;
        }

        _selectedLanguageCode = Localizer.CurrentLanguageCode;
        OnPropertyChanged(nameof(SelectedLanguageCode));
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(SessionModeText));
        OnPropertyChanged(nameof(SessionModeDescription));
        OnPropertyChanged(nameof(ModePanelTitleText));
        OnPropertyChanged(nameof(SessionSourceCaption));
        OnPropertyChanged(nameof(ThemeModeText));
        OnPropertyChanged(nameof(LastReceivedAtText));
        OnPropertyChanged(nameof(TotalReceivedCountText));
        OnPropertyChanged(nameof(SelectedTopicMessageCountText));
        OnPropertyChanged(nameof(AllMessagesResultCountText));
        OnPropertyChanged(nameof(AllMessagesFilteredCountText));
        OnPropertyChanged(nameof(SelectedMessagesCountText));
        OnPropertyChanged(nameof(SelectionColumnWidth));
        OnPropertyChanged(nameof(VehicleFilterSummary));
        OnPropertyChanged(nameof(MessageTypeFilterSummary));
        OnPropertyChanged(nameof(SelectedMessageJsonStatus));
        OnPropertyChanged(nameof(SelectedTopicDisplayText));
        if (TotalReceivedCount == 0)
        {
            SessionSourceText = IsImportedSession ? T("NoFileLoaded") : T("NoBrokerConnected");
        }

        RebuildSelectedMessageDiff();
    }

    public void UpdatePassword(string? password)
    {
        ConnectionSettings.Password = password;
    }

    private bool CanConnect() => !IsImportedSession && _connectionStatus is ConnectionStatus.Disconnected or ConnectionStatus.Error;

    private bool CanDisconnect() => !IsImportedSession && _connectionStatus == ConnectionStatus.Connected;

    private bool CanImport() => IsImportedSession && _connectionStatus != ConnectionStatus.Connecting;

    private bool CanOpenPublishDialog() => !IsImportedSession && IsConnected;

    private bool CanExport()
    {
        return SelectedExportScope switch
        {
            "SelectedMessage" => SelectedMessage is not null,
            "SelectedMessages" => IsSelectionMode && SelectedMessagesCount > 0,
            "SelectedTopic" => SelectedTopic is not null && CurrentTopicMessages.Count > 0,
            "FilteredAllMessages" => AllMessagesFilteredCount > 0,
            _ => TotalReceivedCount > 0
        };
    }

    private bool CanPublish() => !IsImportedSession && IsConnected && !string.IsNullOrWhiteSpace(PublishTopic);

    private bool CanManipulateJsonTree() => SelectedMessage?.IsJson == true && _rawJsonTreeNodes.Count > 0;

    private async Task ConnectAsync()
    {
        if (IsImportedSession)
        {
            StatusText = T("StatusImportedModeActive");
            return;
        }

        if (string.IsNullOrWhiteSpace(ConnectionSettings.Host))
        {
            StatusText = T("StatusHostRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(ConnectionSettings.TopicFilter))
        {
            StatusText = T("StatusTopicFilterRequired");
            return;
        }

        try
        {
            StatusText = TF("StatusConnecting", ConnectionSettings.Host, ConnectionSettings.Port);
            await _mqttMonitorService.ConnectAsync(ConnectionSettings);
            SessionSourceText = $"{ConnectionSettings.Host}:{ConnectionSettings.Port} | {ConnectionSettings.TopicFilter}";
            StatusText = TF("StatusConnected", ConnectionSettings.TopicFilter);
        }
        catch (Exception ex)
        {
            StatusText = TF("StatusConnectionFailed", ex.Message);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _mqttMonitorService.DisconnectAsync();
            IsPublishDialogOpen = false;
            StatusText = T("StatusDisconnected");
        }
        catch (Exception ex)
        {
            StatusText = TF("StatusDisconnectFailed", ex.Message);
        }
    }

    private void OpenPublishDialog()
    {
        PublishTopic = SelectedMessage?.TopicName ?? string.Empty;
        PublishPayload = SelectedMessage?.PayloadRaw ?? string.Empty;
        SelectedPublishQos = SelectedMessage?.Qos.ToString() ?? "0";
        PublishRetain = SelectedMessage?.Retain ?? false;
        IsPublishDialogOpen = true;
    }

    private void ClosePublishDialog()
    {
        IsPublishDialogOpen = false;
    }

    private async Task ImportAsync()
    {
        if (!IsImportedSession)
        {
            StatusText = T("StatusSwitchToImportedFirst");
            return;
        }

        if (TotalReceivedCount > 0 &&
            !ConfirmSessionChange(
                T("DialogReplaceImportedTitle"),
                T("DialogReplaceImportedMessage")))
        {
            StatusText = T("StatusImportCanceled");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Supported files (*.jsonl;*.json;*.txt)|*.jsonl;*.json;*.txt|JSON Lines (*.jsonl)|*.jsonl|JSON (*.json)|*.json|Text (*.txt)|*.txt",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            StatusText = T("StatusNoFileSelected");
            return;
        }

        try
        {
            if (_mqttMonitorService.IsConnected)
            {
                await _mqttMonitorService.DisconnectAsync();
            }

            var importedMessages = await LoadMessagesFromFileAsync(dialog.FileName);

            ResetLoadedData();
            IsImportedSession = true;
            SessionSourceText = Path.GetFileName(dialog.FileName);

            foreach (var message in importedMessages)
            {
                AddMessageToSession(message, highlightTopic: false, updateStatus: false);
            }

            TopicsView.Refresh();
            SelectedTopic = Topics.FirstOrDefault();
            StatusText = TF("StatusImportedCount", importedMessages.Count, dialog.FileName);
            UpdateCommandState();
        }
        catch (Exception ex)
        {
            StatusText = TF("StatusImportFailed", ex.Message);
        }
    }

    private async Task SwitchToImportedSessionAsync()
    {
        try
        {
            if (IsImportedSession)
            {
                StatusText = T("StatusImportedAlreadyActive");
                return;
            }

            if ((TotalReceivedCount > 0 || _mqttMonitorService.IsConnected) &&
                !ConfirmSessionChange(
                    T("DialogSwitchImportedTitle"),
                    T("DialogSwitchImportedMessage")))
            {
                StatusText = T("StatusSessionModeCanceled");
                return;
            }

            if (_mqttMonitorService.IsConnected)
            {
                await _mqttMonitorService.DisconnectAsync();
            }

            ResetLoadedData();
            IsImportedSession = true;
            SessionSourceText = T("NoFileLoaded");
            StatusText = T("StatusImportedReady");
            UpdateCommandState();
        }
        catch (Exception ex)
        {
            StatusText = TF("StatusSwitchModeFailed", ex.Message);
        }
    }

    private void StartLiveSession()
    {
        if (!IsImportedSession)
        {
            StatusText = T("StatusLiveAlreadyActive");
            return;
        }

        if ((TotalReceivedCount > 0 || _mqttMonitorService.IsConnected) &&
            !ConfirmSessionChange(
                T("DialogSwitchLiveTitle"),
                T("DialogSwitchLiveMessage")))
        {
            StatusText = T("StatusSessionModeCanceled");
            return;
        }

        ResetLoadedData();
        IsImportedSession = false;
        SessionSourceText = T("NoBrokerConnected");
        StatusText = T("StatusLiveReady");
        UpdateCommandState();
    }

    private static bool ConfirmSessionChange(string title, string message)
    {
        return MessageBox.Show(
                   message,
                   title,
                   MessageBoxButton.OKCancel,
                   MessageBoxImage.Warning) == MessageBoxResult.OK;
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatus status)
    {
        _dispatcher.Invoke(() =>
        {
            _connectionStatus = status;
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(IsConnected));
            UpdateCommandState();
        });
    }

    private void OnConnectionErrorOccurred(object? sender, string errorMessage)
    {
        _dispatcher.Invoke(() => StatusText = TF("StatusError", errorMessage));
    }

    private void OnMessageReceived(object? sender, IncomingMqttMessage incoming)
    {
        _dispatcher.Invoke(() => AddIncomingMessageCore(incoming));
    }

    private void AddIncomingMessageCore(IncomingMqttMessage incoming)
    {
        var payload = incoming.Payload ?? string.Empty;
        var (isJson, prettyJson) = TryPrettyJson(payload);

        var message = new ReceivedMessage
        {
            TopicName = incoming.Topic ?? string.Empty,
            ReceivedAt = incoming.ReceivedAt,
            Qos = incoming.Qos,
            Retain = incoming.Retain,
            PayloadSize = Encoding.UTF8.GetByteCount(payload),
            PayloadRaw = payload,
            PayloadPrettyJson = prettyJson,
            IsJson = isJson
        };

        AddMessageToSession(message, highlightTopic: true, updateStatus: true);
    }

    private void AddMessageToSession(ReceivedMessage message, bool highlightTopic, bool updateStatus)
    {
        message.PropertyChanged += OnMessagePropertyChanged;

        if (!_messagesByTopic.TryGetValue(message.TopicName, out var messageList))
        {
            messageList = [];
            _messagesByTopic[message.TopicName] = messageList;
        }

        messageList.Add(message);
        AllMessages.Add(message);
        RebuildAllMessageFilterOptions();

        if (!_topicByName.TryGetValue(message.TopicName, out var topicSummary))
        {
            topicSummary = new TopicSummary
            {
                TopicName = message.TopicName
            };

            _topicByName[message.TopicName] = topicSummary;
            Topics.Add(topicSummary);
        }

        topicSummary.MessageCount = messageList.Count;
        topicSummary.LastReceivedAt = message.ReceivedAt;
        topicSummary.VdaGroupName = GuessVdaGroup(message.TopicName);
        topicSummary.VehicleKey = GuessVehicleKey(message.TopicName);
        ApplyTopicDirectionMetadata(topicSummary, message.TopicName);

        if (highlightTopic)
        {
            TriggerTopicHighlight(topicSummary);
        }

        TotalReceivedCount += 1;
        LastReceivedAt = message.ReceivedAt;
        RefreshAllMessagesViewState();

        if (updateStatus)
        {
            StatusText = TF("StatusLastMessage", message.TopicName, message.ReceivedAt);
        }

        if (SelectedTopic?.TopicName == message.TopicName)
        {
            CurrentTopicMessages.Add(message);
            OnPropertyChanged(nameof(SelectedTopicMessageCount));
            OnPropertyChanged(nameof(SelectedTopicMessageCountText));
        }

        TopicsView.Refresh();
        UpdateCommandState();
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ReceivedMessage.IsSelected))
        {
            return;
        }

        OnSelectedMessagesChanged();
    }

    private void OnSelectedMessagesChanged()
    {
        OnPropertyChanged(nameof(SelectedMessagesCount));
        OnPropertyChanged(nameof(SelectedMessagesCountText));
        ClearSelectedMessagesCommand.RaiseCanExecuteChanged();
        ClearMessageSelectionCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
    }

    private static (bool IsJson, string? PrettyJson) TryPrettyJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return (false, null);
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(payload);
            var prettyJson = JsonSerializer.Serialize(jsonDocument.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return (true, prettyJson);
        }
        catch
        {
            return (false, null);
        }
    }

    private void RebuildCurrentTopicMessages()
    {
        CurrentTopicMessages.Clear();
        SelectedMessage = null;

        if (SelectedTopic is null || !_messagesByTopic.TryGetValue(SelectedTopic.TopicName, out var messages))
        {
            return;
        }

        foreach (var message in messages)
        {
            CurrentTopicMessages.Add(message);
        }

        OnPropertyChanged(nameof(SelectedTopicMessageCount));
        OnPropertyChanged(nameof(SelectedTopicMessageCountText));
        ApplyMessageSorting();
    }

    private void RebuildJsonTree()
    {
        _rawJsonTreeNodes.Clear();
        JsonTreeNodes.Clear();

        if (SelectedMessage is null || !SelectedMessage.IsJson)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(SelectedMessage.PayloadRaw);
            _rawJsonTreeNodes.Add(CreateJsonTreeNode("$", document.RootElement, isExpanded: true, lazyLoadChildren: true));
            CollapseJsonTreeToRoot();
        }
        catch
        {
            _rawJsonTreeNodes.Clear();
            JsonTreeNodes.Clear();
        }
    }

    private JsonTreeNode CreateJsonTreeNode(string key, JsonElement element, bool isExpanded, bool lazyLoadChildren)
    {
        var node = new JsonTreeNode
        {
            Key = key,
            ValueKind = element.ValueKind.ToString()
        };

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                node.Value = "{ }";
                break;

            case JsonValueKind.Array:
                node.Value = $"[{element.GetArrayLength()}]";
                break;

            case JsonValueKind.String:
                node.Value = $"\"{element.GetString()}\"";
                break;

            case JsonValueKind.Number:
                node.Value = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                node.Value = element.GetBoolean().ToString();
                break;

            case JsonValueKind.Null:
                node.Value = "null";
                break;

            default:
                node.Value = element.GetRawText();
                break;
        }

        node.ConfigureSource(element, lazyLoadChildren, lazyLoadChildren ? PopulateJsonTreeChildren : null);
        if (!lazyLoadChildren)
        {
            PopulateJsonTreeChildren(node);
        }

        node.IsExpanded = isExpanded;
        return node;
    }

    private void PopulateJsonTreeChildren(JsonTreeNode node)
    {
        if (node.Children.Count > 0)
        {
            node.MarkChildrenLoaded();
            return;
        }

        switch (node.SourceElement.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in node.SourceElement.EnumerateObject())
                {
                    node.Children.Add(CreateJsonTreeNode(property.Name, property.Value, isExpanded: false, lazyLoadChildren: true));
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var arrayItem in node.SourceElement.EnumerateArray())
                {
                    node.Children.Add(CreateJsonTreeNode($"[{index}]", arrayItem, isExpanded: false, lazyLoadChildren: true));
                    index++;
                }
                break;
        }

        node.MarkChildrenLoaded();
    }

    private void ApplyJsonTreeFilter()
    {
        JsonTreeNodes.Clear();

        if (_rawJsonTreeNodes.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(JsonSearchText))
        {
            foreach (var root in _rawJsonTreeNodes)
            {
                JsonTreeNodes.Add(root);
            }

            return;
        }

        var query = JsonSearchText.Trim();
        foreach (var root in _rawJsonTreeNodes)
        {
            var filteredRoot = FilterSubtree(root, query);
            if (filteredRoot is not null)
            {
                JsonTreeNodes.Add(filteredRoot);
            }
        }
    }

    private JsonTreeNode? FilterSubtree(JsonTreeNode source, string query)
    {
        var isMatch =
            source.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            source.Value.Contains(query, StringComparison.OrdinalIgnoreCase);

        var filteredNode = CreateJsonTreeNode(source.Key, source.SourceElement, isExpanded: true, lazyLoadChildren: true);
        filteredNode.IsMatch = isMatch;
        filteredNode.Children.Clear();
        filteredNode.MarkChildrenLoaded();

        foreach (var (childKey, childElement) in EnumerateJsonChildren(source.SourceElement))
        {
            var childNode = CreateJsonTreeNode(childKey, childElement, isExpanded: false, lazyLoadChildren: true);
            var filteredChild = FilterSubtree(childNode, query);
            if (filteredChild is not null)
            {
                filteredNode.Children.Add(filteredChild);
            }
        }

        return isMatch || filteredNode.Children.Count > 0 ? filteredNode : null;
    }

    private void CollapseJsonTreeToRoot()
    {
        foreach (var root in _rawJsonTreeNodes)
        {
            root.IsExpanded = true;
            foreach (var child in root.Children)
            {
                SetJsonTreeExpanded(child, false);
            }
        }

        ApplyJsonTreeFilter();
    }

    private void ExpandJsonTree()
    {
        foreach (var root in _rawJsonTreeNodes)
        {
            SetJsonTreeExpanded(root, true);
        }

        ApplyJsonTreeFilter();
    }

    private static void SetJsonTreeExpanded(JsonTreeNode node, bool isExpanded)
    {
        node.IsExpanded = isExpanded;
        foreach (var child in node.Children)
        {
            SetJsonTreeExpanded(child, isExpanded);
        }
    }

    private static IEnumerable<(string Key, JsonElement Element)> EnumerateJsonChildren(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return (property.Name, property.Value);
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var arrayItem in element.EnumerateArray())
        {
            yield return ($"[{index}]", arrayItem);
            index++;
        }
    }

    private void RebuildSelectedMessageDiff()
    {
        SelectedMessageDiffLines.Clear();

        if (SelectedMessage is null)
        {
            SelectedMessageDiffSummary = T("StatusNoMessageSelected");
            return;
        }

        var previousMessage = FindPreviousMessage(SelectedMessage);
        if (previousMessage is null)
        {
            SelectedMessageDiffSummary = T("DiffNoPrevious");
            return;
        }

        var oldText = GetComparablePayload(previousMessage);
        var newText = GetComparablePayload(SelectedMessage);
        var diff = LineDiffBuilder.Build(oldText, newText);

        SelectedMessageDiffSummary = TF("DiffCompared", previousMessage.ReceivedAt, diff.Added, diff.Removed, diff.Unchanged);

        foreach (var line in diff.Text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (line.Length == 0)
            {
                continue;
            }

            SelectedMessageDiffLines.Add(new DiffLine
            {
                Kind = line.StartsWith("+ ", StringComparison.Ordinal)
                    ? "Added"
                    : line.StartsWith("- ", StringComparison.Ordinal)
                        ? "Removed"
                        : "Context",
                Text = line
            });
        }
    }

    private ReceivedMessage? FindPreviousMessage(ReceivedMessage selectedMessage)
    {
        if (!_messagesByTopic.TryGetValue(selectedMessage.TopicName, out var topicMessages) || topicMessages.Count == 0)
        {
            return null;
        }

        var index = topicMessages.FindIndex(message => message.Id == selectedMessage.Id);
        if (index > 0)
        {
            return topicMessages[index - 1];
        }

        if (index < 0)
        {
            return topicMessages
                .Where(message => message.ReceivedAt < selectedMessage.ReceivedAt)
                .OrderByDescending(message => message.ReceivedAt)
                .FirstOrDefault();
        }

        return null;
    }

    private static string GetComparablePayload(ReceivedMessage message)
    {
        return message.IsJson && !string.IsNullOrWhiteSpace(message.PayloadPrettyJson)
            ? message.PayloadPrettyJson
            : message.PayloadRaw;
    }

    private bool TopicFilterPredicate(object item)
    {
        if (item is not TopicSummary topic)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(TopicSearchText))
        {
            return true;
        }

        return topic.TopicName.Contains(TopicSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyTopicGroupingAndSorting()
    {
        using (TopicsView.DeferRefresh())
        {
            TopicsView.GroupDescriptions.Clear();
            TopicsView.SortDescriptions.Clear();

            if (SelectedTopicGroupMode == "Vda5050Group")
            {
                TopicsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TopicSummary.VdaGroupName)));
            }
            else if (SelectedTopicGroupMode == "Vehicle")
            {
                TopicsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TopicSummary.VehicleKey)));
            }

            if (SelectedTopicSortMode == "TopicNameAsc")
            {
                TopicsView.SortDescriptions.Add(new SortDescription(nameof(TopicSummary.TopicName), ListSortDirection.Ascending));
            }
            else
            {
                TopicsView.SortDescriptions.Add(new SortDescription(nameof(TopicSummary.LastReceivedAt), ListSortDirection.Descending));
            }
        }
    }

    private void ApplyMessageSorting()
    {
        using (MessagesView.DeferRefresh())
        {
            MessagesView.SortDescriptions.Clear();
            MessagesView.SortDescriptions.Add(
                new SortDescription(
                    nameof(ReceivedMessage.ReceivedAt),
                    SelectedMessageSortMode == "OldestFirst"
                        ? ListSortDirection.Ascending
                        : ListSortDirection.Descending));
        }
    }

    private void ApplyAllMessagesSorting()
    {
        using (AllMessagesView.DeferRefresh())
        {
            AllMessagesView.SortDescriptions.Clear();
            AllMessagesView.SortDescriptions.Add(new SortDescription(nameof(ReceivedMessage.ReceivedAt), ListSortDirection.Descending));
        }
    }

    private bool AllMessagesFilterPredicate(object item)
    {
        if (item is not ReceivedMessage message)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(AllMessagesFilterText))
        {
            return MatchesSelectedAllMessageFilters(message);
        }

        return MatchesSelectedAllMessageFilters(message) &&
               message.PayloadRaw.Contains(AllMessagesFilterText, StringComparison.OrdinalIgnoreCase);
    }

    private void ClearAllMessagesFilter()
    {
        AllMessagesFilterText = string.Empty;
        foreach (var option in VehicleFilterOptions.Concat(MessageTypeFilterOptions))
        {
            option.IsSelected = false;
        }

        IsVehicleFilterPopupOpen = false;
        IsMessageTypeFilterPopupOpen = false;
    }

    private string BuildFilterSummary(HashSet<string> selectedValues)
    {
        return selectedValues.Count switch
        {
            0 => T("All"),
            1 => selectedValues.First(),
            _ => selectedValues.Count.ToString()
        };
    }

    private bool HasAnyAllMessagesFilter()
    {
        return !string.IsNullOrWhiteSpace(AllMessagesFilterText) ||
               _selectedVehicleFilters.Count > 0 ||
               _selectedMessageTypeFilters.Count > 0;
    }

    private bool MatchesSelectedAllMessageFilters(ReceivedMessage message)
    {
        return (_selectedVehicleFilters.Count == 0 || _selectedVehicleFilters.Contains(message.VehicleKey)) &&
               (_selectedMessageTypeFilters.Count == 0 || _selectedMessageTypeFilters.Contains(message.MessageType));
    }

    private void RebuildAllMessageFilterOptions()
    {
        var selectedVehicles = VehicleFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .ToHashSet(StringComparer.Ordinal);
        var selectedMessageTypes = MessageTypeFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .ToHashSet(StringComparer.Ordinal);

        ReplaceFilterOptions(
            VehicleFilterOptions,
            AllMessages
                .GroupBy(message => message.VehicleKey, StringComparer.Ordinal)
                .OrderBy(group => group.Key)
                .Select(group => new MessageFilterOption(group.Key, group.Count())),
            selectedVehicles);

        ReplaceFilterOptions(
            MessageTypeFilterOptions,
            AllMessages
                .GroupBy(message => message.MessageType, StringComparer.Ordinal)
                .OrderBy(group => group.Key)
                .Select(group => new MessageFilterOption(group.Key, group.Count())),
            selectedMessageTypes);

        RebuildSelectedAllMessageFilterSets();
        RefreshAllMessagesViewState();
    }

    private void ReplaceFilterOptions(
        ObservableCollection<MessageFilterOption> target,
        IEnumerable<MessageFilterOption> source,
        HashSet<string> selectedValues)
    {
        foreach (var option in target)
        {
            option.PropertyChanged -= OnAllMessageFilterOptionPropertyChanged;
        }

        target.Clear();

        foreach (var option in source)
        {
            option.IsSelected = selectedValues.Contains(option.Value);
            option.PropertyChanged += OnAllMessageFilterOptionPropertyChanged;
            target.Add(option);
        }
    }

    private void OnAllMessageFilterOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MessageFilterOption.IsSelected))
        {
            return;
        }

        RebuildSelectedAllMessageFilterSets();
        RefreshAllMessagesViewState();
    }

    private void RebuildSelectedAllMessageFilterSets()
    {
        _selectedVehicleFilters.Clear();
        foreach (var option in VehicleFilterOptions.Where(option => option.IsSelected))
        {
            _selectedVehicleFilters.Add(option.Value);
        }

        _selectedMessageTypeFilters.Clear();
        foreach (var option in MessageTypeFilterOptions.Where(option => option.IsSelected))
        {
            _selectedMessageTypeFilters.Add(option.Value);
        }
    }

    private void RefreshAllMessagesViewState()
    {
        AllMessagesView.Refresh();
        OnPropertyChanged(nameof(AllMessagesFilteredCount));
        OnPropertyChanged(nameof(AllMessagesResultCountText));
        OnPropertyChanged(nameof(AllMessagesFilteredCountText));
        OnPropertyChanged(nameof(VehicleFilterSummary));
        OnPropertyChanged(nameof(MessageTypeFilterSummary));
        ClearAllMessagesFilterCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
    }

    private void TriggerTopicHighlight(TopicSummary topicSummary)
    {
        if (_highlightByTopic.TryGetValue(topicSummary.TopicName, out var previousTokenSource))
        {
            previousTokenSource.Cancel();
            previousTokenSource.Dispose();
        }

        topicSummary.IsHighlighted = true;

        var tokenSource = new CancellationTokenSource();
        _highlightByTopic[topicSummary.TopicName] = tokenSource;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500), tokenSource.Token);
                await _dispatcher.InvokeAsync(() => topicSummary.IsHighlighted = false);
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private static string GuessVdaGroup(string topic)
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

    private static void ApplyTopicDirectionMetadata(TopicSummary topicSummary, string topicName)
    {
        var vdaGroup = GuessVdaGroup(topicName);

        (topicSummary.PublisherLabel, topicSummary.DirectionLabel) = vdaGroup switch
        {
            "order" => ("FMS", "FMS → Robot"),
            "instantActions" => ("FMS", "FMS → Robot"),
            "zoneSet" => ("FMS", "FMS → Robot"),
            "responses" => ("FMS", "FMS → Robot"),
            "state" => ("Robot", "Robot → FMS"),
            "factsheet" => ("Robot", "Robot → FMS"),
            "visualization" => ("Robot", "Robot → Viewer"),
            "connection" => ("Broker/Robot", "Broker/Robot → FMS"),
            _ => ("Unknown", "Unknown")
        };
    }

    private void ClearAllMessages()
    {
        ResetLoadedData();
        StatusText = IsImportedSession ? T("StatusClearedImported") : T("StatusClearedAll");
        UpdateCommandState();
    }

    private void ClearSelectedTopicMessages()
    {
        if (SelectedTopic is null)
        {
            return;
        }

        var topicName = SelectedTopic.TopicName;
        if (_messagesByTopic.TryGetValue(topicName, out var removedMessages))
        {
            TotalReceivedCount = Math.Max(0, TotalReceivedCount - removedMessages.Count);
            foreach (var message in removedMessages)
            {
                message.PropertyChanged -= OnMessagePropertyChanged;
                AllMessages.Remove(message);
            }

            removedMessages.Clear();
        }
        else
        {
            _messagesByTopic[topicName] = [];
        }

        if (_topicByName.TryGetValue(topicName, out var topicSummary))
        {
            topicSummary.MessageCount = 0;
            topicSummary.LastReceivedAt = null;
            topicSummary.VehicleKey = GuessVehicleKey(topicName);
            topicSummary.VdaGroupName = GuessVdaGroup(topicName);
            ApplyTopicDirectionMetadata(topicSummary, topicName);
        }

        if (_highlightByTopic.Remove(topicName, out var tokenSource))
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
        }

        CurrentTopicMessages.Clear();
        SelectedMessage = null;
        SelectedMessageDiffLines.Clear();
        SelectedMessageDiffSummary = T("StatusNoMessageSelected");
        RecalculateLastReceivedAt();
        TopicsView.Refresh();
        RebuildAllMessageFilterOptions();
        OnPropertyChanged(nameof(SelectedTopicMessageCount));
        OnPropertyChanged(nameof(SelectedTopicMessageCountText));
        OnSelectedMessagesChanged();
        StatusText = TF("StatusClearedTopic", topicName);
        UpdateCommandState();
    }

    private void ClearSelectedMessage()
    {
        if (SelectedMessage is null)
        {
            return;
        }

        var selectedMessage = SelectedMessage;
        if (!RemoveMessageFromSession(selectedMessage))
        {
            return;
        }

        FinishMessageRemoval();
        StatusText = T("StatusClearedMessage");
    }

    private void ClearSelectedMessages()
    {
        var selectedMessages = AllMessages.Where(message => message.IsSelected).ToList();
        if (selectedMessages.Count == 0)
        {
            return;
        }

        var removedCount = 0;
        foreach (var message in selectedMessages)
        {
            if (RemoveMessageFromSession(message))
            {
                removedCount += 1;
            }
        }

        FinishMessageRemoval();
        StatusText = TF("StatusClearedSelectedMessages", removedCount);
    }

    private void ClearMessageSelection()
    {
        foreach (var message in AllMessages.Where(message => message.IsSelected).ToList())
        {
            message.IsSelected = false;
        }
    }

    private bool HasSelectedMessages() => IsSelectionMode && SelectedMessagesCount > 0;

    private bool RemoveMessageFromSession(ReceivedMessage message)
    {
        if (!_messagesByTopic.TryGetValue(message.TopicName, out var topicMessages))
        {
            return false;
        }

        var removedMessage = message;
        var removed = topicMessages.Remove(removedMessage);
        if (!removed)
        {
            var existing = topicMessages.FirstOrDefault(candidate => candidate.Id == message.Id);
            if (existing is not null)
            {
                removedMessage = existing;
                removed = topicMessages.Remove(existing);
            }
        }

        if (!removed)
        {
            return false;
        }

        removedMessage.PropertyChanged -= OnMessagePropertyChanged;
        TotalReceivedCount = Math.Max(0, TotalReceivedCount - 1);
        AllMessages.Remove(removedMessage);

        if (SelectedTopic?.TopicName == removedMessage.TopicName)
        {
            CurrentTopicMessages.Remove(removedMessage);
        }

        if (_topicByName.TryGetValue(removedMessage.TopicName, out var topicSummary))
        {
            if (topicMessages.Count == 0)
            {
                if (SelectedTopic?.TopicName == removedMessage.TopicName)
                {
                    topicSummary.MessageCount = 0;
                    topicSummary.LastReceivedAt = null;
                }
                else
                {
                    _messagesByTopic.Remove(removedMessage.TopicName);
                    _topicByName.Remove(removedMessage.TopicName);
                    Topics.Remove(topicSummary);

                    if (_highlightByTopic.Remove(removedMessage.TopicName, out var tokenSource))
                    {
                        tokenSource.Cancel();
                        tokenSource.Dispose();
                    }
                }
            }
            else
            {
                topicSummary.MessageCount = topicMessages.Count;
                topicSummary.LastReceivedAt = topicMessages[^1].ReceivedAt;
            }
        }

        if (SelectedMessage?.Id == removedMessage.Id)
        {
            SelectedMessage = null;
            SelectedMessageDiffLines.Clear();
            SelectedMessageDiffSummary = T("StatusNoMessageSelected");
        }

        return true;
    }

    private void FinishMessageRemoval()
    {
        RecalculateLastReceivedAt();
        TopicsView.Refresh();
        RebuildAllMessageFilterOptions();
        OnPropertyChanged(nameof(SelectedTopicMessageCount));
        OnPropertyChanged(nameof(SelectedTopicMessageCountText));
        OnSelectedMessagesChanged();
        UpdateCommandState();
    }

    private void ResetLoadedData()
    {
        foreach (var tokenSource in _highlightByTopic.Values)
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
        }

        _highlightByTopic.Clear();
        foreach (var message in AllMessages)
        {
            message.PropertyChanged -= OnMessagePropertyChanged;
        }

        _messagesByTopic.Clear();
        _topicByName.Clear();
        Topics.Clear();
        CurrentTopicMessages.Clear();
        AllMessages.Clear();
        JsonTreeNodes.Clear();
        _rawJsonTreeNodes.Clear();
        SelectedMessageDiffLines.Clear();
        SelectedTopic = null;
        SelectedMessage = null;
        SelectedMessageDiffSummary = T("StatusNoMessageSelected");
        TotalReceivedCount = 0;
        LastReceivedAt = null;
        AllMessagesFilterText = string.Empty;
        RebuildAllMessageFilterOptions();
        OnSelectedMessagesChanged();
    }

    private static async Task<List<ReceivedMessage>> LoadMessagesFromFileAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jsonl" => await LoadJsonLinesAsync(filePath),
            ".json" => await LoadJsonAsync(filePath),
            ".txt" => await LoadTextExportAsync(filePath),
            _ => throw new InvalidOperationException("Unsupported import format.")
        };
    }

    private static async Task<List<ReceivedMessage>> LoadJsonLinesAsync(string filePath)
    {
        var messages = new List<ReceivedMessage>();
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);

        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var message = JsonSerializer.Deserialize<ReceivedMessage>(line);
            if (message is not null)
            {
                messages.Add(NormalizeImportedMessage(message));
            }
        }

        return messages;
    }

    private static async Task<List<ReceivedMessage>> LoadJsonAsync(string filePath)
    {
        var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var messages = JsonSerializer.Deserialize<List<ReceivedMessage>>(text) ?? [];
        return messages.Select(NormalizeImportedMessage).ToList();
    }

    private static async Task<List<ReceivedMessage>> LoadTextExportAsync(string filePath)
    {
        var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var separator = $"{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
        var blocks = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var messages = new List<ReceivedMessage>();

        foreach (var block in blocks)
        {
            var lines = block.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0)
            {
                continue;
            }

            var headerParts = lines[0].Split(" | ", StringSplitOptions.None);
            if (headerParts.Length < 4)
            {
                continue;
            }

            var qosText = headerParts[2].Replace("QoS=", string.Empty, StringComparison.OrdinalIgnoreCase);
            var retainText = headerParts[3].Replace("Retain=", string.Empty, StringComparison.OrdinalIgnoreCase);

            var rawMessage = new ReceivedMessage
            {
                ReceivedAt = DateTime.TryParse(headerParts[0], out var receivedAt) ? receivedAt : DateTime.Now,
                TopicName = headerParts[1],
                Qos = int.TryParse(qosText, out var qos) ? qos : 0,
                Retain = bool.TryParse(retainText, out var retain) && retain,
                PayloadRaw = string.Join(Environment.NewLine, lines.Skip(1))
            };

            messages.Add(NormalizeImportedMessage(rawMessage));
        }

        return messages;
    }

    private static ReceivedMessage NormalizeImportedMessage(ReceivedMessage message)
    {
        var payload = message.PayloadRaw ?? string.Empty;
        var (isJson, prettyJson) = TryPrettyJson(payload);

        return new ReceivedMessage
        {
            Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id,
            TopicName = message.TopicName ?? string.Empty,
            ReceivedAt = message.ReceivedAt == default ? DateTime.Now : message.ReceivedAt,
            Qos = message.Qos,
            Retain = message.Retain,
            PayloadSize = Encoding.UTF8.GetByteCount(payload),
            PayloadRaw = payload,
            PayloadPrettyJson = prettyJson ?? message.PayloadPrettyJson,
            IsJson = isJson
        };
    }

    private void RecalculateLastReceivedAt()
    {
        LastReceivedAt = _messagesByTopic.Values
            .SelectMany(messages => messages)
            .Select(message => (DateTime?)message.ReceivedAt)
            .Max();
    }

    private void CopyRawPayload()
    {
        if (SelectedMessage is null)
        {
            return;
        }

        Clipboard.SetText(SelectedMessage.PayloadRaw);
        StatusText = T("StatusRawCopied");
    }

    private void CopyPrettyPayload()
    {
        if (SelectedMessage is null)
        {
            return;
        }

        Clipboard.SetText(SelectedMessagePrettyText);
        StatusText = T("StatusPrettyCopied");
    }

    private void CopyMessageWithMetadata()
    {
        if (SelectedMessage is null)
        {
            return;
        }

        var text = string.Join(Environment.NewLine, new[]
        {
            $"Topic: {SelectedMessage.TopicName}",
            $"ReceivedAt: {SelectedMessage.ReceivedAt:yyyy-MM-dd HH:mm:ss.fff}",
            $"QoS: {SelectedMessage.Qos}",
            $"Retain: {SelectedMessage.Retain}",
            $"PayloadSize: {SelectedMessage.PayloadSize}",
            "Payload:",
            SelectedMessage.PayloadRaw
        });

        Clipboard.SetText(text);
        StatusText = T("StatusFullCopied");
    }

    private async Task ExportAsync()
    {
        var messages = GetMessagesForExport().ToList();
        if (messages.Count == 0)
        {
            StatusText = T("StatusNoMessagesToExport");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON Lines (*.jsonl)|*.jsonl|JSON (*.json)|*.json|Text (*.txt)|*.txt",
            FileName = $"mqtt-export-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if (extension == ".json")
        {
            var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(dialog.FileName, json, Encoding.UTF8);
        }
        else if (extension == ".txt")
        {
            var text = string.Join(
                $"{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}",
                messages.Select(message =>
                    $"{message.ReceivedAt:yyyy-MM-dd HH:mm:ss.fff} | {message.TopicName} | QoS={message.Qos} | Retain={message.Retain}{Environment.NewLine}{message.PayloadRaw}"));

            await File.WriteAllTextAsync(dialog.FileName, text, Encoding.UTF8);
        }
        else
        {
            var lines = messages.Select(message => JsonSerializer.Serialize(message));
            await File.WriteAllLinesAsync(dialog.FileName, lines, Encoding.UTF8);
        }

        StatusText = TF("StatusExported", messages.Count, dialog.FileName);
    }

    private async Task PublishAsync()
    {
        if (!CanPublish())
        {
            StatusText = T("StatusPublishTopicRequired");
            return;
        }

        try
        {
            await _mqttMonitorService.PublishAsync(
                PublishTopic.Trim(),
                PublishPayload,
                int.TryParse(SelectedPublishQos, out var qos) ? qos : 0,
                PublishRetain);

            IsPublishDialogOpen = false;
            StatusText = TF("StatusPublished", PublishTopic.Trim());
        }
        catch (Exception ex)
        {
            StatusText = TF("StatusPublishFailed", ex.Message);
        }
    }

    private IEnumerable<ReceivedMessage> GetMessagesForExport()
    {
        return SelectedExportScope switch
        {
            "SelectedMessage" when SelectedMessage is not null => [SelectedMessage],
            "SelectedMessages" => AllMessages.Where(message => message.IsSelected).OrderBy(message => message.ReceivedAt),
            "SelectedTopic" when SelectedTopic is not null && _messagesByTopic.TryGetValue(SelectedTopic.TopicName, out var topicMessages) => topicMessages,
            "FilteredAllMessages" => AllMessagesView.Cast<ReceivedMessage>(),
            _ => AllMessages.OrderBy(message => message.ReceivedAt)
        };
    }

    private void UpdateCommandState()
    {
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        SwitchToImportedSessionCommand.RaiseCanExecuteChanged();
        ImportCommand.RaiseCanExecuteChanged();
        StartLiveSessionCommand.RaiseCanExecuteChanged();
        ClearAllCommand.RaiseCanExecuteChanged();
        ClearSelectedTopicCommand.RaiseCanExecuteChanged();
        ClearSelectedMessageCommand.RaiseCanExecuteChanged();
        ClearSelectedMessagesCommand.RaiseCanExecuteChanged();
        ClearMessageSelectionCommand.RaiseCanExecuteChanged();
        ClearAllMessagesFilterCommand.RaiseCanExecuteChanged();
        OpenPublishDialogCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        PublishCommand.RaiseCanExecuteChanged();
        CopyRawCommand.RaiseCanExecuteChanged();
        CopyPrettyCommand.RaiseCanExecuteChanged();
        CopyFullCommand.RaiseCanExecuteChanged();
        CollapseJsonTreeToRootCommand.RaiseCanExecuteChanged();
        ExpandJsonTreeCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        Localizer.PropertyChanged -= OnLocalizerPropertyChanged;
        _mqttMonitorService.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _mqttMonitorService.ConnectionErrorOccurred -= OnConnectionErrorOccurred;
        _mqttMonitorService.MessageReceived -= OnMessageReceived;

        foreach (var tokenSource in _highlightByTopic.Values)
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
        }

        foreach (var message in AllMessages)
        {
            message.PropertyChanged -= OnMessagePropertyChanged;
        }

        _highlightByTopic.Clear();
        _mqttMonitorService.Dispose();
    }
}
