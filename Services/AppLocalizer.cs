using System.Globalization;
using MqttViewer.Infrastructure;

namespace MqttViewer.Services;

public sealed class AppLocalizer : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "VDA 5050 MQTT Monitor",
                ["AppSubtitle"] = "Mode-aware MQTT inspection, imported file playback, and message diffing.",
                ["SessionMode"] = "Session Mode",
                ["SessionLive"] = "Live MQTT",
                ["SessionImported"] = "Imported File",
                ["SessionLiveDescription"] = "Subscribe to broker messages in real time using the connection settings on the right.",
                ["SessionImportedDescription"] = "Load a saved export and inspect only that file. MQTT stays disconnected in this mode.",
                ["ThemeLight"] = "Light",
                ["ThemeDark"] = "Dark",
                ["LanguageEnglish"] = "English",
                ["LanguageKorean"] = "Korean",
                ["Connection"] = "Connection",
                ["ImportedFile"] = "Imported File",
                ["ConnectionHint"] = "Connect uses the broker settings above and starts the live stream.",
                ["ImportedHint"] = "Open a saved export file. While this mode is active, only imported messages are shown.",
                ["ImportedReplaceHint"] = "Opening another file replaces the current imported session after confirmation.",
                ["Data"] = "Data",
                ["ExportScope"] = "Export Scope",
                ["Export"] = "Export",
                ["TopicExplorer"] = "Topic Explorer",
                ["ClearAll"] = "Clear All",
                ["ClearTopic"] = "Clear Topic",
                ["SearchTopic"] = "Search topic",
                ["Group"] = "Group",
                ["Sort"] = "Sort",
                ["NoTopicSelected"] = "No topic selected",
                ["ClearMessage"] = "Clear Message",
                ["ReceivedAt"] = "Received At",
                ["Size"] = "Size",
                ["Retain"] = "Retain",
                ["Preview"] = "Preview",
                ["MessageDetail"] = "Message Detail",
                ["CopyRaw"] = "Copy Raw",
                ["CopyPretty"] = "Copy Pretty",
                ["CopyFull"] = "Copy Full",
                ["Topic"] = "Topic",
                ["PayloadSize"] = "Payload Size",
                ["Qos"] = "QoS",
                ["JsonStatus"] = "JSON Status",
                ["JsonTree"] = "JSON Tree",
                ["SearchJsonTree"] = "Search JSON tree",
                ["CollapseToRoot"] = "Collapse to Root",
                ["ExpandAll"] = "Expand All",
                ["Diff"] = "Diff",
                ["DiffTitle"] = "Selected vs Previous In Topic",
                ["DiffLegend"] = "Legend: green = added, red = removed, neutral = unchanged",
                ["PrettyJson"] = "Pretty JSON",
                ["Raw"] = "Raw",
                ["LastReceivedLabel"] = "Last",
                ["TotalLabel"] = "Total: {0}",
                ["SelectedTopicLabel"] = "Selected Topic: {0}",
                ["Host"] = "Host",
                ["Port"] = "Port",
                ["TopicFilter"] = "Topic Filter",
                ["ClientIdOptional"] = "Client ID (optional)",
                ["Username"] = "Username",
                ["Password"] = "Password",
                ["Connect"] = "Connect",
                ["Disconnect"] = "Disconnect",
                ["OpenFile"] = "Open File...",
                ["StatusDisconnected"] = "Disconnected",
                ["ConnectionStatusConnected"] = "Connected",
                ["ConnectionStatusConnecting"] = "Connecting",
                ["ConnectionStatusError"] = "Error",
                ["NoBrokerConnected"] = "No broker connected",
                ["NoFileLoaded"] = "No file loaded",
                ["StatusImportedModeActive"] = "Imported File mode is active. Switch to Live MQTT mode to connect.",
                ["StatusHostRequired"] = "Host is required.",
                ["StatusTopicFilterRequired"] = "Topic filter is required.",
                ["StatusConnecting"] = "Connecting to {0}:{1}...",
                ["StatusConnected"] = "Connected. Subscribed to '{0}'.",
                ["StatusConnectionFailed"] = "Connection failed: {0}",
                ["StatusDisconnectFailed"] = "Disconnect failed: {0}",
                ["StatusSwitchToImportedFirst"] = "Switch to Imported File mode first.",
                ["StatusImportCanceled"] = "Import canceled.",
                ["StatusNoFileSelected"] = "No file selected. Imported File mode is still active.",
                ["StatusImportedCount"] = "Imported {0} message(s) from '{1}'.",
                ["StatusImportFailed"] = "Import failed: {0}",
                ["StatusImportedAlreadyActive"] = "Imported File mode is already active.",
                ["StatusSessionModeCanceled"] = "Session mode change canceled.",
                ["StatusImportedReady"] = "Imported File mode is ready. Open a file to inspect saved messages.",
                ["StatusSwitchModeFailed"] = "Could not switch session mode: {0}",
                ["StatusLiveAlreadyActive"] = "Live MQTT mode is already active.",
                ["StatusLiveReady"] = "Live MQTT mode is ready. Connect when you want to start streaming.",
                ["StatusError"] = "Error: {0}",
                ["StatusLastMessage"] = "Last message: {0} @ {1:HH:mm:ss.fff}",
                ["StatusClearedImported"] = "Cleared imported messages.",
                ["StatusClearedAll"] = "Cleared all messages.",
                ["StatusClearedTopic"] = "Cleared topic '{0}'.",
                ["StatusClearedMessage"] = "Selected message cleared.",
                ["StatusRawCopied"] = "Raw payload copied.",
                ["StatusPrettyCopied"] = "Pretty payload copied.",
                ["StatusFullCopied"] = "Message + metadata copied.",
                ["StatusNoMessagesToExport"] = "No messages to export.",
                ["StatusExported"] = "Exported {0} message(s) to '{1}'.",
                ["StatusNoMessageSelected"] = "No message selected",
                ["StatusJsonParsed"] = "JSON parsed successfully",
                ["StatusInvalidJson"] = "Invalid JSON payload (showing raw text)",
                ["DiffNoPrevious"] = "No previous message in this topic.",
                ["DiffCompared"] = "Compared with {0:HH:mm:ss.fff} | +{1} / -{2} / ={3}",
                ["DialogReplaceImportedTitle"] = "Replace Imported Session",
                ["DialogReplaceImportedMessage"] = "Opening a new file will replace the current imported messages. Continue?",
                ["DialogSwitchImportedTitle"] = "Switch To Imported File",
                ["DialogSwitchImportedMessage"] = "Switching to Imported File mode will disconnect MQTT and clear the current session data. Continue?",
                ["DialogSwitchLiveTitle"] = "Switch To Live MQTT",
                ["DialogSwitchLiveMessage"] = "Switching to Live MQTT mode will clear the current imported or buffered messages. Continue?",
                ["SortRecentFirst"] = "Recent First",
                ["SortTopicNameAsc"] = "Topic Name Asc",
                ["GroupNone"] = "None",
                ["GroupVda5050"] = "VDA 5050 Group",
                ["GroupVehicle"] = "Vehicle",
                ["SortNewestFirst"] = "Newest First",
                ["SortOldestFirst"] = "Oldest First",
                ["ExportSelectedMessage"] = "Selected Message",
                ["ExportSelectedTopic"] = "Selected Topic",
                ["ExportAllTopics"] = "All Topics"
            },
            ["ko"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AppTitle"] = "VDA 5050 MQTT 모니터",
                ["AppSubtitle"] = "세션 모드에 따라 MQTT 실시간 메시지와 가져온 파일을 확인하고 diff까지 비교합니다.",
                ["SessionMode"] = "세션 모드",
                ["SessionLive"] = "실시간 MQTT",
                ["SessionImported"] = "가져온 파일",
                ["SessionLiveDescription"] = "오른쪽 연결 설정을 사용해 브로커 메시지를 실시간으로 구독합니다.",
                ["SessionImportedDescription"] = "저장된 export 파일만 불러와 확인합니다. 이 모드에서는 MQTT 연결이 비활성화됩니다.",
                ["ThemeLight"] = "라이트",
                ["ThemeDark"] = "다크",
                ["LanguageEnglish"] = "English",
                ["LanguageKorean"] = "한국어",
                ["Connection"] = "연결",
                ["ImportedFile"] = "가져온 파일",
                ["ConnectionHint"] = "위 설정으로 브로커에 연결하고 실시간 스트림을 시작합니다.",
                ["ImportedHint"] = "저장된 export 파일을 열어 확인합니다. 이 모드에서는 가져온 메시지만 표시됩니다.",
                ["ImportedReplaceHint"] = "다른 파일을 열면 현재 가져온 세션이 확인 후 교체됩니다.",
                ["Data"] = "데이터",
                ["ExportScope"] = "내보내기 범위",
                ["Export"] = "내보내기",
                ["TopicExplorer"] = "토픽 탐색기",
                ["ClearAll"] = "전체 비우기",
                ["ClearTopic"] = "토픽 비우기",
                ["SearchTopic"] = "토픽 검색",
                ["Group"] = "그룹",
                ["Sort"] = "정렬",
                ["NoTopicSelected"] = "선택된 토픽 없음",
                ["ClearMessage"] = "메시지 삭제",
                ["ReceivedAt"] = "수신 시각",
                ["Size"] = "크기",
                ["Retain"] = "Retain",
                ["Preview"] = "미리보기",
                ["MessageDetail"] = "메시지 상세",
                ["CopyRaw"] = "원본 복사",
                ["CopyPretty"] = "정리본 복사",
                ["CopyFull"] = "전체 복사",
                ["Topic"] = "토픽",
                ["PayloadSize"] = "Payload 크기",
                ["Qos"] = "QoS",
                ["JsonStatus"] = "JSON 상태",
                ["JsonTree"] = "JSON 트리",
                ["SearchJsonTree"] = "JSON 트리 검색",
                ["CollapseToRoot"] = "루트까지만 접기",
                ["ExpandAll"] = "모두 펼치기",
                ["Diff"] = "Diff",
                ["DiffTitle"] = "선택 메시지와 이전 메시지 비교",
                ["DiffLegend"] = "범례: 초록 = 추가, 빨강 = 삭제, 기본 = 동일",
                ["PrettyJson"] = "정리된 JSON",
                ["Raw"] = "원본",
                ["LastReceivedLabel"] = "마지막 수신",
                ["TotalLabel"] = "전체: {0}",
                ["SelectedTopicLabel"] = "선택 토픽: {0}",
                ["Host"] = "호스트",
                ["Port"] = "포트",
                ["TopicFilter"] = "토픽 필터",
                ["ClientIdOptional"] = "클라이언트 ID (선택)",
                ["Username"] = "사용자명",
                ["Password"] = "비밀번호",
                ["Connect"] = "연결",
                ["Disconnect"] = "해제",
                ["OpenFile"] = "파일 열기...",
                ["StatusDisconnected"] = "연결 해제됨",
                ["ConnectionStatusConnected"] = "연결됨",
                ["ConnectionStatusConnecting"] = "연결 중",
                ["ConnectionStatusError"] = "오류",
                ["NoBrokerConnected"] = "연결된 브로커 없음",
                ["NoFileLoaded"] = "불러온 파일 없음",
                ["StatusImportedModeActive"] = "가져온 파일 모드가 활성화되어 있습니다. 실시간 MQTT 모드로 전환 후 연결하세요.",
                ["StatusHostRequired"] = "Host는 필수입니다.",
                ["StatusTopicFilterRequired"] = "Topic Filter는 필수입니다.",
                ["StatusConnecting"] = "{0}:{1}에 연결 중...",
                ["StatusConnected"] = "'{0}' 구독이 완료되었습니다.",
                ["StatusConnectionFailed"] = "연결 실패: {0}",
                ["StatusDisconnectFailed"] = "연결 해제 실패: {0}",
                ["StatusSwitchToImportedFirst"] = "먼저 가져온 파일 모드로 전환하세요.",
                ["StatusImportCanceled"] = "가져오기를 취소했습니다.",
                ["StatusNoFileSelected"] = "선택된 파일이 없습니다. 가져온 파일 모드는 유지됩니다.",
                ["StatusImportedCount"] = "'{1}'에서 {0}개 메시지를 가져왔습니다.",
                ["StatusImportFailed"] = "가져오기 실패: {0}",
                ["StatusImportedAlreadyActive"] = "이미 가져온 파일 모드입니다.",
                ["StatusSessionModeCanceled"] = "세션 모드 전환을 취소했습니다.",
                ["StatusImportedReady"] = "가져온 파일 모드 준비 완료. 파일을 열어 저장된 메시지를 확인하세요.",
                ["StatusSwitchModeFailed"] = "세션 모드 전환 실패: {0}",
                ["StatusLiveAlreadyActive"] = "이미 실시간 MQTT 모드입니다.",
                ["StatusLiveReady"] = "실시간 MQTT 모드 준비 완료. 연결하면 스트림이 시작됩니다.",
                ["StatusError"] = "오류: {0}",
                ["StatusLastMessage"] = "마지막 메시지: {0} @ {1:HH:mm:ss.fff}",
                ["StatusClearedImported"] = "가져온 메시지를 비웠습니다.",
                ["StatusClearedAll"] = "전체 메시지를 비웠습니다.",
                ["StatusClearedTopic"] = "'{0}' 토픽을 비웠습니다.",
                ["StatusClearedMessage"] = "선택한 메시지를 삭제했습니다.",
                ["StatusRawCopied"] = "원본 payload를 복사했습니다.",
                ["StatusPrettyCopied"] = "정리된 payload를 복사했습니다.",
                ["StatusFullCopied"] = "메시지와 메타데이터를 복사했습니다.",
                ["StatusNoMessagesToExport"] = "내보낼 메시지가 없습니다.",
                ["StatusExported"] = "'{1}'로 {0}개 메시지를 내보냈습니다.",
                ["StatusNoMessageSelected"] = "선택된 메시지가 없습니다",
                ["StatusJsonParsed"] = "JSON 파싱 성공",
                ["StatusInvalidJson"] = "유효하지 않은 JSON입니다. 원본 텍스트를 표시합니다",
                ["DiffNoPrevious"] = "이 토픽에는 이전 메시지가 없습니다.",
                ["DiffCompared"] = "{0:HH:mm:ss.fff} 메시지와 비교 | +{1} / -{2} / ={3}",
                ["DialogReplaceImportedTitle"] = "가져온 세션 교체",
                ["DialogReplaceImportedMessage"] = "새 파일을 열면 현재 가져온 메시지가 교체됩니다. 계속할까요?",
                ["DialogSwitchImportedTitle"] = "가져온 파일 모드로 전환",
                ["DialogSwitchImportedMessage"] = "가져온 파일 모드로 전환하면 MQTT 연결이 종료되고 현재 세션 데이터가 비워집니다. 계속할까요?",
                ["DialogSwitchLiveTitle"] = "실시간 MQTT 모드로 전환",
                ["DialogSwitchLiveMessage"] = "실시간 MQTT 모드로 전환하면 현재 가져온 데이터 또는 버퍼 메시지가 비워집니다. 계속할까요?",
                ["SortRecentFirst"] = "최근순",
                ["SortTopicNameAsc"] = "토픽 이름순",
                ["GroupNone"] = "없음",
                ["GroupVda5050"] = "VDA 5050 그룹",
                ["GroupVehicle"] = "Vehicle",
                ["SortNewestFirst"] = "최신순",
                ["SortOldestFirst"] = "오래된순",
                ["ExportSelectedMessage"] = "선택 메시지",
                ["ExportSelectedTopic"] = "선택 토픽",
                ["ExportAllTopics"] = "전체 토픽"
            }
        };

    private string _currentLanguageCode;

    private AppLocalizer()
    {
        var cultureCode = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        _currentLanguageCode = Resources.ContainsKey(cultureCode) ? cultureCode : "en";
    }

    public static AppLocalizer Instance { get; } = new();

    public string CurrentLanguageCode
    {
        get => _currentLanguageCode;
        set
        {
            var normalized = Resources.ContainsKey(value) ? value : "en";
            if (!SetProperty(ref _currentLanguageCode, normalized))
            {
                return;
            }

            OnPropertyChanged("Item[]");
        }
    }

    public string this[string key]
    {
        get
        {
            if (Resources.TryGetValue(CurrentLanguageCode, out var selected) &&
                selected.TryGetValue(key, out var localized))
            {
                return localized;
            }

            if (Resources["en"].TryGetValue(key, out var fallback))
            {
                return fallback;
            }

            return key;
        }
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, this[key], args);
    }
}
