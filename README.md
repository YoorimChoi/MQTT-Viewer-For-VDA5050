# MQTT Viewer (VDA 5050 MQTT Monitor)

VDA 5050 토픽 기반 메시지를 실시간으로 모니터링하고, 저장된 메시지 파일을 다시 불러와 분석할 수 있는 WPF 데스크톱 도구입니다.

## 1) 주요 기능

- Live MQTT 모드: 브로커에 연결해 토픽을 실시간 구독
- Imported File 모드: 저장된 메시지 파일(`.jsonl`, `.json`, `.txt`)을 오프라인 분석
- Topic Explorer
  - 토픽 검색
  - 그룹핑(None / VDA 5050 Group / Vehicle)
  - 정렬(최근순 / 토픽명 오름차순)
- Message List
  - 토픽별 메시지 목록 확인
  - 메시지 정렬(최신순 / 오래된순)
- Message Detail
  - JSON Tree 검색
  - 이전 메시지 대비 Diff(+/-/unchanged)
  - Pretty JSON / Raw 탭
  - Raw / Pretty / Full(메타데이터 포함) 복사
- Export
  - 범위 선택(선택 메시지 / 선택 토픽 / 전체 토픽)
  - 형식 선택(`.jsonl`, `.json`, `.txt`)
- 부가 기능
  - 다국어(en/ko) 전환
  - Light / Dark 테마 전환

## 2) 기술 스택

- .NET: `net10.0-windows`
- UI: WPF + MaterialDesignThemes
- MQTT: MQTTnet
- 프로토콜: MQTT `3.1.1`

## 3) 실행 방법

### 요구 사항

- Windows
- .NET SDK 10.0 이상

### 실행

프로젝트 루트(`MqttViewer.csproj`가 있는 폴더)에서:

```powershell
dotnet restore
dotnet run
```

또는 워크스페이스 루트(`D:\00.Code\MqttViewer`)에서:

```powershell
dotnet run --project .\MqttViewer\MqttViewer.csproj
```

## 4) 빠른 사용법

### A. Live MQTT 모드

1. 상단의 Session Mode에서 `Live MQTT` 선택
2. 연결 정보 입력
   - Host (기본: `127.0.0.1`)
   - Port (기본: `1883`)
   - Topic Filter (기본: `#`)
   - Client ID (비우면 자동 생성)
   - Username / Password (옵션)
3. `Connect` 클릭
4. 왼쪽 Topic Explorer에서 토픽 선택
5. 가운데 메시지 목록에서 항목 선택 후 오른쪽 상세 탭(JSON Tree / Diff / Pretty / Raw) 확인
6. 필요 시 `Disconnect`

### B. Imported File 모드

1. Session Mode에서 `Imported File` 선택
2. `Open File...` 클릭
3. 파일 선택(`.jsonl`, `.json`, `.txt`)
4. Live 모드와 동일하게 토픽/메시지/상세 분석

참고:
- 모드 전환 시 현재 세션 데이터가 초기화될 수 있어 확인 팝업이 표시됩니다.
- Imported File 모드에서는 MQTT 연결이 비활성화됩니다.

## 5) Export / Import 포맷

### Export 범위

- Selected Message
- Selected Topic
- All Topics

### Export 형식

#### 1) JSONL (`.jsonl`, 기본)

메시지 1건당 1줄(JSON 객체):

```json
{"Id":"...","TopicName":"uagv/v1/...","ReceivedAt":"2026-04-24T09:30:11.123","Qos":0,"Retain":false,"PayloadSize":245,"PayloadRaw":"{...}","PayloadPrettyJson":"{\n  ...\n}","IsJson":true}
```

#### 2) JSON (`.json`)

메시지 배열(JSON array):

```json
[
  {
    "Id": "...",
    "TopicName": "uagv/v1/...",
    "ReceivedAt": "2026-04-24T09:30:11.123",
    "Qos": 0,
    "Retain": false,
    "PayloadSize": 245,
    "PayloadRaw": "{...}",
    "PayloadPrettyJson": "{\n  ...\n}",
    "IsJson": true
  }
]
```

#### 3) Text (`.txt`)

아래 블록이 메시지 단위로 구분되어 저장:

```text
2026-04-24 09:30:11.123 | uagv/v1/... | QoS=0 | Retain=False
{ ...payload... }
--------------------------------------------------------------------------------
2026-04-24 09:30:12.456 | uagv/v1/... | QoS=0 | Retain=False
{ ...payload... }
```

### Import 지원 형식

- `.jsonl`: 각 줄을 `ReceivedMessage`로 역직렬화
- `.json`: `ReceivedMessage[]` 배열 역직렬화
- `.txt`: Export 텍스트 포맷 파싱

## 6) 화면 구성 요약

- 상단 바
  - 연결 상태
  - 현재 세션 모드
  - 프로토콜 버전 표시
  - 언어 선택, 테마 토글
- 좌측 패널: 세션 모드/연결 또는 파일 불러오기, Export 설정
- 중앙 하단 3분할
  - Topic Explorer
  - Message List
  - Message Detail
- 하단 상태 바
  - 현재 상태 메시지
  - 전체 메시지 수 / 선택 토픽 메시지 수 / 마지막 수신 시각

## 7) 운영 팁

- Topic Filter를 좁혀서 구독하면 성능과 가독성이 좋아집니다.
- Diff 탭은 동일 토픽의 이전 메시지와 비교합니다.
- JSON Tree 검색을 사용하면 큰 payload에서 키/값 찾기가 빠릅니다.
- `Clear All`, `Clear Topic`, `Clear Message`는 현재 세션 데이터만 정리합니다.
