# Device Layer Implementation Plan (Keyboard QC Station)

> Kế hoạch triển khai tầng `Device` cho dự án **Custom Keyboard Builder**.
> Nguồn yêu cầu: `Documents_Refactor/IoT_Device_Layer_Simulation_Guide.md`.
> Tài liệu này ánh xạ đề xuất trong guide vào **kiến trúc thực tế** của codebase và mô tả
> các bước có thể thực thi trực tiếp mà không cần đọc lại toàn bộ source.
>
> **Bản revision 2** — đã sửa theo review (xem [§12 Change log](#12-change-log-so-với-bản-đầu)).
> Nhóm sửa **critical** (bắt buộc trước khi code): transport MQTT, vòng đời session/FK,
> abstraction telemetry, đồng bộ schema↔model.

---

## 1. Overview & Scope

Thêm một tầng `Device` để **mô phỏng trạm QC kiểm tra keyboard** sau khi seller lắp build.
Không cần thiết bị vật lý — `DeviceSimulator` sinh dữ liệu test từng phím gồm:

- **Signal test** — phím có nhận tín hiệu press/release đúng không (NoSignal / WrongKey / Chatter / StuckKey).
- **Latency test** — độ trễ; ngưỡng **chặt hơn cho HE switch** (gaming/rapid trigger).
- **Noise test** — độ ồn (dB), đối chiếu yêu cầu "im lặng" của buyer.

### Nguyên tắc kiến trúc

- **SQL Server là source of truth.** Mọi kết quả test phải được lưu DB trước, realtime chỉ là bonus.
- **MQTT là transport chính** (theo yêu cầu đã chốt & guide §2/§9.0):
  `DeviceSimulator → MQTT broker → App subscriber → DeviceService → SQL Server`.
- **In-process chỉ là fallback** cho unit test / demo nhanh khi máy không có broker — simulator gọi
  thẳng `DeviceService`. Khi không cấu hình broker hoặc publish MQTT thất bại, simulator phải chuyển sang
  direct-call path (`DeviceService.RecordKeyResultAsync` / `CompleteSessionAsync`), **không** được chỉ no-op vì
  như vậy sẽ mất dữ liệu QC.
- **Telemetry device tách riêng khỏi tầng realtime request** — không tái dùng `IRealtimeNotifier`
  (xem [§6.3](#63-vì-sao-không-tái-dùng-irealtimenotifier)).

### In scope (theo guide §15)

- Bảng `devices`, `device_test_sessions`, `device_key_test_results`.
- Simulator sinh data; service áp dụng rule pass/fail; UI tóm tắt QC cho seller/buyer.
- Cặp publisher/subscriber telemetry qua MQTT + fallback in-process.

### Out of scope (phase này)

- Kết nối keyboard vật lý / đọc HID-USB / micro thật / robot bấm phím / AI chọn switch phức tạp.
- **Buyer requirement UI/schema** (noise/latency do buyer nhập) — chưa có trong dự án, để phase sau.
  Phase này dùng **requirement mặc định** (xem [§6.1](#61-deviceqcrules-static-thuần--không-io)).

---

## 2. Vị trí trong kiến trúc hiện tại

Dự án là **WPF (`net10.0-windows`), MVVM, SQL Server, KHÔNG dùng DI container** — mọi thứ wire thủ công trong `MainWindow.xaml.cs`. Tầng Device tái sử dụng nguyên các pattern sau:

| Thành phần guide | Ánh xạ vào codebase | Pattern tái sử dụng |
| --- | --- | --- |
| Bảng DB | `Database/SqlServer/CreateSchema_Refactor.sql` | snake_case, idempotent drop→create→index |
| Model | `Models/Devices/` (`Custom_keyboard.Models.Devices`) | POCO; enum lưu `VARCHAR` |
| Repository | `Repositories/IDeviceRepository.cs` + `Repositories/SqlServer/` | ADO.NET thuần như `SqlRequestRepository` |
| Service + rule | `Services/Devices/` | interface + `sealed` impl, async, persist trước rồi publish best-effort (như `RequestService`) |
| Simulator | `Services/Devices/DeviceSimulator.cs` | `Task.Run(..., CancellationToken.None)` như `MqttRealtimeService` |
| Telemetry transport | `Realtime/` (đã có MQTTnet + `MqttSettings`) | **mô phỏng** cặp `IRealtimeNotifier`/`IRealtimeSubscriber`/`MqttRealtimeService` thành cặp telemetry **mới** |
| UI | `ViewModels/SellerDashboardViewModel.cs`, `Views/SellerDashboardView.xaml`, buyer tương ứng | `ViewModelBase`, `AsyncRelayCommand`, DataGrid, `SelectedRequest` |
| Màu trạng thái | `Converters/StatusConverters.cs` | `StatusToBrushConverter` |
| Localization | `Localization/AppStrings.cs` | cặp (vi,en) + `{loc:Tr Key}` / `Tr(...)` |

### Shape of the change

```mermaid
flowchart TD
    SELLERVM[SellerDashboardViewModel<br/>StartQcTestCommand] -->|1. StartSessionAsync| DSVC
    SELLERVM -->|2. start simulator| SIM[DeviceSimulator<br/>Services/Devices]

    subgraph Main["Đường chính — MQTT"]
        SIM -->|publish key-test / summary| PUB[IDeviceTelemetryPublisher<br/>Mqtt*]
        PUB --> BROKER[(MQTT broker)]
        BROKER --> SUB[IDeviceTelemetrySubscriber<br/>Mqtt*]
        SUB -->|RecordKeyResult / CompleteSession| DSVC[DeviceService + DeviceQcRules<br/>Services/Devices]
    end

    SIM -. fallback khi không có broker .->|gọi thẳng| DSVC

    DSVC -->|persist only; no telemetry re-publish| DREPO[Device repositories<br/>Repositories/SqlServer]
    DREPO -->|INSERT/UPDATE| DB[(SQL Server<br/>devices / device_test_sessions /<br/>device_key_test_results)]

    SUB -. reload event .-> SELLERVM
    SELLERVM --> SELLERV[SellerDashboardView<br/>per-key DataGrid + summary]
    DSVC --> BUYERVM[BuyerDashboardViewModel<br/>SelectedRequest → summary] --> BUYERV[BuyerDashboardView]

    style Main fill:#eef7ee,stroke:#5a5
```

**Thứ tự triển khai (mỗi phase build trên phase trước):**
**Schema → Models/Enums → Repositories (+SqlTableNames) → Rules+Service (gồm vòng đời session) → Telemetry abstraction → Simulator → MainWindow wiring → UI/Localization.**

---

## 3. Phase 1 — Database

**File:** `Database/SqlServer/CreateSchema_Refactor.sql` (script idempotent, DB `CustomKeyboard_Refactor`).

> ⚠️ Script này **destructive**: nó drop toàn bộ 18 bảng theo thứ tự ngược FK rồi tạo lại. Thêm bảng mới phải đặt đúng vị trí trong cả khối DROP lẫn CREATE; chạy lại sẽ reset + reseed. Sau khi thêm 3 bảng device, tổng là **21 bảng** — cập nhật cả comment "drops the 18 tables" ở đầu file.

### 3.1 Thêm vào khối DROP (đầu file, vì 3 bảng này là leaf — drop trước `chat_messages`)

```sql
DROP TABLE IF EXISTS device_key_test_results;
DROP TABLE IF EXISTS device_test_sessions;
DROP TABLE IF EXISTS devices;
-- ... các DROP hiện có (chat_messages, chat_conversations, ...) giữ nguyên bên dưới
```

### 3.2 Thêm CREATE TABLE (sau `build_requests`, trước/đan xen khối chat — miễn sau `users` & `build_requests`)

```sql
-- ===========================================================================
-- devices  (trạm QC của seller)
-- ===========================================================================
CREATE TABLE devices (
    device_id        VARCHAR(50)  PRIMARY KEY,        -- vd DEV_{Guid:N} hoặc 'QC-STATION-01'
    seller_user_id   INT          NOT NULL,
    device_name      NVARCHAR(100) NOT NULL,
    device_type      VARCHAR(50)  NOT NULL,           -- QC_STATION (gộp) / KEY_SIGNAL_TESTER / LATENCY_TESTER / NOISE_SENSOR
    is_active        BIT          NOT NULL DEFAULT 1,
    last_seen_at     DATETIME2    NULL,
    created_at       DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_devices_seller FOREIGN KEY (seller_user_id) REFERENCES users(user_id),
    CONSTRAINT CK_devices_type CHECK (device_type IN ('QC_STATION','KEY_SIGNAL_TESTER','LATENCY_TESTER','NOISE_SENSOR'))
);
GO

-- ===========================================================================
-- device_test_sessions  (một phiên QC cho một request)
-- ===========================================================================
CREATE TABLE device_test_sessions (
    session_id         VARCHAR(50) PRIMARY KEY,        -- vd QCSESS_{Guid:N}
    request_id         VARCHAR(50) NOT NULL,
    device_id          VARCHAR(50) NOT NULL,
    seller_user_id     INT         NOT NULL,
    switch_technology  VARCHAR(50) NULL,               -- Mechanical / HE (lấy từ kit.pcbTechnology)
    total_keys         INT         NOT NULL,
    tested_keys        INT         NOT NULL DEFAULT 0,
    passed_keys        INT         NOT NULL DEFAULT 0,
    warning_keys       INT         NOT NULL DEFAULT 0,
    failed_keys        INT         NOT NULL DEFAULT 0,
    average_latency_ms DECIMAL(8,2) NULL,
    max_latency_ms     DECIMAL(8,2) NULL,
    average_noise_db   DECIMAL(8,2) NULL,
    max_noise_db       DECIMAL(8,2) NULL,
    status             VARCHAR(20) NOT NULL,           -- Running / Passed / Warning / Failed
    started_at         DATETIME2   NOT NULL DEFAULT SYSUTCDATETIME(),
    completed_at       DATETIME2   NULL,
    CONSTRAINT FK_dts_request FOREIGN KEY (request_id) REFERENCES build_requests(request_id),
    CONSTRAINT FK_dts_device  FOREIGN KEY (device_id)  REFERENCES devices(device_id),
    CONSTRAINT FK_dts_seller  FOREIGN KEY (seller_user_id) REFERENCES users(user_id),
    CONSTRAINT CK_dts_status CHECK (status IN ('Running','Passed','Warning','Failed'))
);
GO

-- ===========================================================================
-- device_key_test_results  (kết quả từng phím — bảng chi tiết chính)
-- ===========================================================================
CREATE TABLE device_key_test_results (
    key_test_id             BIGINT IDENTITY(1,1) PRIMARY KEY,
    session_id              VARCHAR(50) NOT NULL,
    request_id              VARCHAR(50) NOT NULL,
    device_id               VARCHAR(50) NOT NULL,
    key_code                VARCHAR(30) NOT NULL,
    expected_key            VARCHAR(30) NOT NULL,
    received_key            VARCHAR(30) NULL,
    press_signal_detected   BIT         NOT NULL,
    latency_ms              DECIMAL(8,2) NULL,
    press_event_count       INT         NOT NULL,
    bounce_count            INT         NULL,           -- null khi chưa đủ chu kỳ press-release (vd StuckKey)
    release_signal_detected BIT         NOT NULL,
    hold_duration_ms        INT         NULL,
    is_stuck                BIT         NOT NULL,
    noise_db                DECIMAL(8,2) NULL,
    switch_technology       VARCHAR(50) NULL,           -- khớp guide §6 JSON per-key
    result                  VARCHAR(20) NOT NULL,       -- Pass / Warning / Fail
    failure_type            VARCHAR(30) NULL,           -- NoSignal / WrongKey / Chatter / StuckKey / HighLatency / TooNoisy
    failure_reason          NVARCHAR(255) NULL,
    recorded_at             DATETIME2   NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_dktr_session FOREIGN KEY (session_id) REFERENCES device_test_sessions(session_id),
    CONSTRAINT FK_dktr_request FOREIGN KEY (request_id) REFERENCES build_requests(request_id),
    CONSTRAINT FK_dktr_device  FOREIGN KEY (device_id)  REFERENCES devices(device_id),
    CONSTRAINT CK_dktr_result CHECK (result IN ('Pass','Warning','Fail')),
    CONSTRAINT CK_dktr_failure_type CHECK (
        failure_type IS NULL
        OR failure_type IN ('NoSignal','WrongKey','Chatter','StuckKey','HighLatency','TooNoisy')
    )
);
GO
```

> **[FIX #4]** Đã thêm cột `switch_technology` vào `device_key_test_results` để khớp model
> `DeviceKeyTestResult` và JSON per-key trong guide §6. (Giá trị cũng có ở session-level; lưu thêm
> ở per-key là denormalize có chủ đích để rule engine + UI dùng trực tiếp, không phải join.)
>
> **[FIX #7]** `CK_devices_type` thêm `QC_STATION` cho trạm QC gộp cả 3 chức năng (đúng concept
> `QC-STATION-01` trong guide). Giữ lại 3 loại chuyên biệt cho báo cáo/seed.
>
> **[FIX #8]** Thêm `FK_dktr_request` và `FK_dktr_device` để per-key không trôi dữ liệu so với
> session. Repository vẫn luôn copy `request_id`/`device_id` từ session khi insert (xem §5).
>
> **[FIX thêm]** `failure_type` cũng có CHECK constraint để DB không nhận chuỗi lỗi tự do ngoài các loại
> `NoSignal` / `WrongKey` / `Chatter` / `StuckKey` / `HighLatency` / `TooNoisy`.

### 3.3 Device QC: tạo **on-demand**, KHÔNG seed cứng trong schema

> **[FIX #7/#8 — quan trọng]** `CreateSchema_Refactor.sql` là **pure DDL, KHÔNG seed `users`** (users
> được tạo runtime; `SeedDemoAnalytics_Refactor.sql` khám phá seller động bằng
> `SELECT TOP 1 ... FROM seller_profiles WHERE is_verified=1 AND is_active=1`). Do đó **không** được
> nhét `INSERT INTO devices (... seller_user_id = 2 ...)` vào schema — sẽ vỡ `FK_devices_seller`
> (chưa có user) và hardcode id rất mong manh.

Thay vào đó, app **tạo trạm QC khi cần** — thêm vào `DeviceService` (gọi trước `StartSessionAsync` để
thỏa `FK_dts_device`):

```
GetOrCreateQcStationAsync(sellerUserId)
   → IDeviceRepository.GetBySellerAsync(sellerUserId)
   → trả về device đầu tiên IsActive && DeviceType == QC_STATION
   → nếu chưa có: tạo Device { DeviceId = $"DEV_{Guid:N}", DeviceType = QC_STATION,
                                DeviceName = "Keyboard QC Station", IsActive = true } → SaveAsync
```

Không phụ thuộc seed-script, không lo thứ tự FK. (Tùy chọn: một seed script riêng kiểu
`SeedDemoAnalytics` có thể pre-create trạm QC bằng cách discover verified seller — đánh dấu OPTIONAL,
không bắt buộc cho luồng chạy.)

### 3.4 Thêm vào khối CREATE INDEX (cuối file)

```sql
CREATE INDEX IX_devices_seller ON devices(seller_user_id);
CREATE INDEX IX_dts_request ON device_test_sessions(request_id);
CREATE INDEX IX_dts_device ON device_test_sessions(device_id);
CREATE INDEX IX_dts_seller_status ON device_test_sessions(seller_user_id, status);
CREATE INDEX IX_dktr_session ON device_key_test_results(session_id);
CREATE INDEX IX_dktr_request ON device_key_test_results(request_id);
GO
```

### 3.5 (Tùy chọn — phase sau) Mở rộng catalog & build requirement

Đánh dấu **OPTIONAL** — không cần cho luồng QC lõi:

- `switches`: thêm `noise_profile VARCHAR(20)`, `expected_latency_ms DECIMAL(8,2)`, `is_he BIT`, `actuation_type VARCHAR(50)` (guide §11 — hỗ trợ seller gợi ý switch theo yêu cầu).
- `builds` (hoặc bảng riêng): `switch_selection_mode`, `preferred_switch_profile`, `preferred_noise_level`, `preferred_latency_level`, `requires_he_switch`, `switch_requirement_note` (guide §8.4 — buyer để seller tự chọn switch + nhập yêu cầu noise/latency).

> **[FIX #6]** `preferred_noise_level` / `preferred_latency_level` của buyer **chưa tồn tại** trong
> dự án (không có ở `builds`, `build_requests`, hay UI buyer). Phase QC lõi **không** phụ thuộc các
> field này — dùng requirement mặc định (xem §6.1). Khi nào làm khối OPTIONAL này thì rule mới đọc
> requirement thật từ buyer.

---

## 4. Phase 2 — Models & Enums

**Thư mục:** `Models/Devices/`, namespace `Custom_keyboard.Models.Devices`. POCO theo phong cách `Models/Builds/BuildRequest.cs` (PascalCase ↔ snake_case cột).

### Enums (`Models/Enums/`, lưu `VARCHAR`)

| Enum | Giá trị |
| --- | --- |
| `DeviceType` | `QC_STATION`, `KEY_SIGNAL_TESTER`, `LATENCY_TESTER`, `NOISE_SENSOR` |
| `KeyTestResult` | `Pass`, `Warning`, `Fail` |
| `KeyFailureType` | `NoSignal`, `WrongKey`, `Chatter`, `StuckKey`, `HighLatency`, `TooNoisy` |
| `TestSessionStatus` | `Running`, `Passed`, `Warning`, `Failed` |

> Lưu ý `DeviceType` có gạch dưới: dùng `[Description]` hoặc map thủ công khi ToString/parse để khớp giá trị DB, hoặc theo cách `GetEnumValue<T>` đã tolerant gạch dưới (giống `RequestStatus.In_progress`).

### Model classes

**`Device.cs`**
```
DeviceId (string) · SellerUserId (int) · DeviceName (string) · DeviceType (DeviceType)
IsActive (bool) · LastSeenAt (DateTime?) · CreatedAt (DateTime)
```

**`DeviceTestSession.cs`**
```
SessionId · RequestId · DeviceId · SellerUserId · SwitchTechnology (string?)
TotalKeys · TestedKeys · PassedKeys · WarningKeys · FailedKeys (int)
AverageLatencyMs · MaxLatencyMs · AverageNoiseDb · MaxNoiseDb (decimal?)
Status (TestSessionStatus) · StartedAt (DateTime) · CompletedAt (DateTime?)
```

**`DeviceKeyTestResult.cs`** — ánh xạ JSON guide §6 (đã có cột DB tương ứng ở §3.2):
```
KeyTestId (long) · SessionId · RequestId · DeviceId · KeyCode · ExpectedKey
ReceivedKey (string?) · PressSignalDetected (bool) · LatencyMs (decimal?)
PressEventCount (int) · BounceCount (int?) · ReleaseSignalDetected (bool)
HoldDurationMs (int?) · IsStuck (bool) · NoiseDb (decimal?)
SwitchTechnology (string?) · Result (KeyTestResult) · FailureType (KeyFailureType?)
FailureReason (string?) · RecordedAt (DateTime)
```

**`KeyTelemetry.cs`** (DTO transport, không lưu DB) — payload simulator publish lên MQTT / truyền in-process,
là **input** cho `DeviceQcRules`; chưa có `Result`/`FailureType` (do rule quyết định):
```
SessionId · RequestId · DeviceId · KeyCode · ExpectedKey · ReceivedKey (string?)
PressSignalDetected · LatencyMs (decimal?) · PressEventCount · BounceCount (int?)
ReleaseSignalDetected · HoldDurationMs (int?) · IsStuck · NoiseDb (decimal?) · SwitchTechnology (string?)
```

Các trường nullable bám đúng guide: ví dụ `BounceCount = null` và `LatencyMs/HoldDurationMs = null` khi NoSignal; `BounceCount = null` khi StuckKey (chưa đủ chu kỳ press-release).

---

## 5. Phase 3 — Repositories

**Interfaces** trong `Repositories/`, **impl** trong `Repositories/SqlServer/`. Theo đúng idiom của `SqlRequestRepository`: ctor nhận `ISqlConnectionFactory`, mở `await using var connection = _connectionFactory.CreateConnection(); await connection.OpenAsync(ct);`, dùng `command.AddParameter(...)` và các extension đọc reader (`GetStringValue`, `GetIntValue`, `GetDecimalValue`, `GetBoolValue`, `GetDateTimeValue`, `GetNullableXxx`, `GetEnumValue<T>`) từ `SqlRepositoryHelpers.cs`. Upsert dùng `IF EXISTS … UPDATE … ELSE INSERT`. Sinh PK kiểu `$"DEV_{Guid.NewGuid():N}"`, `$"QCSESS_{Guid.NewGuid():N}"`.

| Interface | Methods chính |
| --- | --- |
| `IDeviceRepository` | `SaveAsync(Device)` (upsert), `GetBySellerAsync(int sellerUserId)`, `GetByIdAsync(string deviceId)` |
| `IDeviceTestSessionRepository` | `SaveAsync(DeviceTestSession)` (upsert — dùng cho **cả** insert lúc Running lẫn update lúc tổng kết), `GetLatestByRequestAsync(string requestId)`, `GetByIdAsync(string sessionId)` |
| `IDeviceKeyTestResultRepository` | `InsertAsync(DeviceKeyTestResult)` (INSERT, IDENTITY), `GetBySessionAsync(string sessionId)` |

> **[FIX #2 + #8]** `IDeviceTestSessionRepository.SaveAsync` phải upsert được để service `StartSessionAsync`
> tạo row `Running` **trước**, rồi `CompleteSessionAsync` mới update. Khi `InsertAsync` per-key, repository
> luôn lấy `request_id`/`device_id` từ session (truyền xuống từ service) để khớp FK & tránh lệch dữ liệu.

**`Data/SqlServer/SqlTableNames.cs`** — thêm:
```csharp
// Device
public const string Devices = "devices";
public const string DeviceTestSessions = "device_test_sessions";
public const string DeviceKeyTestResults = "device_key_test_results";
```

---

## 6. Phase 4 — Rule Engine + Service

**Thư mục:** `Services/Devices/`.

### 6.1 `DeviceQcRules` (static, thuần — không I/O)

Áp dụng ngưỡng guide §5, trả về `(KeyTestResult, KeyFailureType?, string? reason)` cho mỗi phím telemetry:

- **Signal:** `press_signal_detected = false` → Fail/`NoSignal`; `received_key != expected_key` → Fail/`WrongKey`; `release_signal_detected = false` hoặc `is_stuck`/`hold_duration_ms > stuck_threshold` → Fail/`StuckKey`; `bounce_count >= 1` (≥2 press event/chu kỳ) → Fail/`Chatter`.
- **Latency:** Mechanical — `<=10` Pass, `<=20` Warning, `>20` Fail/`HighLatency`; **HE** — `<=3` Pass, `<=6` Warning, `>6` Fail/`HighLatency`.
- **Noise:** nếu buyer yêu cầu silent — `<=45` Pass, `<=55` Warning, `>55` Fail/`TooNoisy`; nếu không yêu cầu — noise chỉ là tham khảo/warning, **không** fail build.
- Ưu tiên kết luận: StuckKey trước Chatter khi thiếu release.

**[FIX #6] Requirement mặc định cho phase này.** Tham số hóa ngưỡng qua một `record QcThresholds`
và một enum `NoiseRequirement { Silent, Quiet, Normal }`:
```
record QcThresholds(
    string SwitchTechnology,          // "Mechanical" | "HE" — input THẬT từ kit.pcbTechnology
    NoiseRequirement NoiseRequirement = NoiseRequirement.Normal,   // mặc định phase này
    int StuckThresholdMs = 1000)      // ví dụ; hold_duration_ms > ngưỡng ⇒ StuckKey
```

Quy ước phase 1 (vì buyer **chưa** có UI nhập yêu cầu):

- **Latency** chỉ phụ thuộc `SwitchTechnology` (HE chặt hơn Mechanical). `buyerLatencyRequirement` **không**
  được dùng ở phase này — chỉ kích hoạt khi làm khối OPTIONAL §3.5.
- **Noise** với `NoiseRequirement = Normal` ⇒ **không bao giờ Fail/`TooNoisy`**; chỉ ghi `noise_db` để
  tham khảo (tối đa là `Warning` thông tin nếu vượt band Normal). `TooNoisy` chỉ xuất hiện khi
  `NoiseRequirement = Silent` (phase sau, khi buyer yêu cầu im lặng).
- Chữ ký `DeviceQcRules.Evaluate(telemetry, thresholds)` **không đổi** khi sau này truyền requirement
  thật vào — chỉ đổi giá trị enum.

`SwitchTechnology` luôn là input thật; chỉ `NoiseRequirement`/`StuckThresholdMs` mới mang giá trị mặc định.

### 6.2 `IDeviceService` / `DeviceService` (`sealed`, async) — vòng đời session rõ ràng

Phụ thuộc: 3 device repository. **Không** phụ thuộc `IRealtimeNotifier` và cũng **không** nhận bất kỳ publisher
telemetry nào (xem §6.3). `DeviceService` chỉ làm nhiệm vụ validate,
persist, và trả kết quả cho caller. Việc publish MQTT thuộc `DeviceSimulator`/publisher; cách tách này tránh
publish-loop hoặc duplicate telemetry.

**[FIX #2] Vòng đời bắt buộc — đúng thứ tự để không vỡ FK:**

```
StartSessionAsync(requestId, sellerUserId, deviceId, switchTechnology, totalKeys)
   → tạo DeviceTestSession { status = Running, tested/passed/... = 0 }
   → SaveAsync (INSERT) → trả về sessionId        // session row tồn tại TRƯỚC

RecordKeyResultAsync(telemetry)        // gọi N lần
   → DeviceQcRules.Evaluate(telemetry, thresholds)
   → dựng DeviceKeyTestResult (copy request_id/device_id từ session)
   → InsertAsync                                  // FK session_id luôn hợp lệ

CompleteSessionAsync(sessionId)
    → GetBySessionAsync → tổng hợp tested/passed/warning/failed, avg/max latency & noise
    → nếu số key result < total_keys thì chưa complete; trả về Running/throw lỗi nghiệp vụ nhẹ để caller retry
    → status theo guide §7: có Fail ⇒ Failed; không Fail nhưng có Warning ⇒ Warning; còn lại ⇒ Passed
      (thêm điều kiện silent/HE nếu buyer yêu cầu)
    → SaveAsync (UPDATE) — set completed_at
```

Cả simulator (in-process) lẫn subscriber (MQTT) đều đi qua đúng 3 bước này. Vì `StartSessionAsync`
chạy ở app **trước khi** simulator publish key-test đầu tiên, FK `session_id` luôn được thỏa bất kể transport.

> **Thứ tự thỏa FK đầy đủ:** `GetOrCreateQcStationAsync(sellerUserId)` (§3.3, đảm bảo device row tồn tại
> ⇒ `FK_dts_device`) **→** `StartSessionAsync` (tạo session ⇒ `FK_dktr_session`) **→** `RecordKeyResultAsync`.
> `DeviceService` cũng thêm method `GetOrCreateQcStationAsync` ngoài 3 method vòng đời trên.

### 6.3 Vì sao KHÔNG tái dùng `IRealtimeNotifier`

> **[FIX #3]** `Realtime/IRealtimeNotifier.cs` hiện chỉ có `RequestCreatedAsync` và
> `RequestStatusChangedAsync` — **không** có method cho telemetry device/QC. Nhồi telemetry vào đó sẽ
> bóp méo abstraction request hiện có. Do đó:
>
> - Định nghĩa **cặp riêng** trong `Realtime/` (hoặc `Realtime/Devices/`), **mô phỏng** cặp hiện có:
>   - `IDeviceTelemetryPublisher`: `Task<bool> PublishKeyTestAsync(KeyTelemetry)`,
>     `Task<bool> PublishSessionSummaryAsync(DeviceTestSession)`. `true` nghĩa là publish thành công; `false`
>     nghĩa là simulator phải fallback direct-call sang `DeviceService`.
>   - `IDeviceTelemetrySubscriber`: event `KeyTestReceived`, `SessionSummaryReceived`; `StartAsync(...)`/`StopAsync()`.
> - `DeviceService` **không** nhận `IRealtimeNotifier` và **không re-publish** telemetry vừa nhận. UI reload qua
>   VM sau khi service persist xong hoặc qua event nội bộ của subscriber, không publish lại cùng topic MQTT.
> - `IRealtimeNotifier`/`IRealtimeSubscriber` **giữ nguyên**, không sửa.

---

## 7. Phase 5 — Telemetry Transport + Device Simulator

### 7.1 Telemetry transport (MQTT chính, in-process fallback)

Tái dùng `MQTTnet` + `MqttSettings` đã có. Tạo trong `Realtime/Devices/`:

- `MqttDeviceTelemetryPublisher : IDeviceTelemetryPublisher` — publish JSON lên và trả `true/false` để simulator
  biết có cần fallback in-process không:
  - `keyboard/device/{deviceId}/request/{requestId}/key-test` (guide §9.1)
  - `keyboard/device/{deviceId}/request/{requestId}/session-summary` (guide §9.2)
  - trả `false` khi `MqttSettings.Enabled = false`, không connect được broker, hoặc publish lỗi.
- `MqttDeviceTelemetrySubscriber : IDeviceTelemetrySubscriber` — subscribe 2 topic trên, deserialize,
  gọi `DeviceService.RecordKeyResultAsync` / `CompleteSessionAsync`.
- `NullDeviceTelemetryPublisher` trả `false` — dùng khi tắt MQTT hoặc không cấu hình broker. Vì trả `false`,
  simulator sẽ ghi trực tiếp qua `DeviceService`, nên dữ liệu QC không bị mất.

**Ordering / duplicate safety:**

- Khi dùng MQTT, app phải tạo và start `MqttDeviceTelemetrySubscriber` trước khi seller bấm Start QC Test.
- Subscriber nên xử lý tuần tự theo `sessionId` (ví dụ queue/semaphore mỗi session) để key-test được insert xong
  trước khi summary được complete.
- Khi nhận session-summary, subscriber chỉ gọi `CompleteSessionAsync(sessionId)` nếu số row key-test đã đạt
  `total_keys`; nếu chưa đủ, delay/retry ngắn hoặc để `CompleteSessionAsync` trả Running để subscriber gọi lại.
- `DeviceService` không publish lại telemetry vừa persist, tránh vòng lặp MQTT và tránh duplicate rows.

### 7.2 `DeviceSimulator`

**File:** `Services/Devices/DeviceSimulator.cs`. Chạy fire-and-forget `Task.Run(async () => {...}, CancellationToken.None)` như `MqttRealtimeService`.

**[FIX #5] Input — lấy từ đâu (cụ thể):**
Simulator **không** cần `IBuildRepository`/`IComponentCatalogService`. Nó parse `BuildRequest.RequestPayloadJson`
(cùng cách `BuildRequestSnapshotFormatter` đọc snapshot bằng `JsonDocument`).

> **Casing JSON:** `RequestService.CreateSnapshotJsonAsync` serialize với
> `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, nên key là **camelCase** (`kit`,
> `requiredSwitchQuantity`, `pcbTechnology`). Dùng helper case-tolerant như
> `BuildRequestSnapshotFormatter.TryGetProperty` (thử cả camelCase lẫn PascalCase) cho an toàn.

| Input simulator cần | Nguồn |
| --- | --- |
| `requiredSwitchQuantity` (số phím) | `kit.requiredSwitchQuantity` trong payload |
| `switchTechnology` (Mechanical/HE) | `kit.pcbTechnology` trong payload (đúng theo `IsCompatibleSwitch`: `switch.SwitchTechnology == kit.PcbTechnology`) |
| `requestId` | `BuildRequest.RequestId` đang chọn |
| `sellerUserId` | `BuildRequest.SellerUserId` |
| `deviceId` | `DeviceService.GetOrCreateQcStationAsync(sellerUserId)` (§3.3) — tạo on-demand, không seed cứng |
| `buyerNoiseRequirement` / `buyerLatencyRequirement` | **chưa có** ⇒ mặc định `Normal` (xem §6.1) |

> **Lý do dùng `kit.pcbTechnology`** thay vì tra switch đã chọn: buyer có thể **không** chọn switch cụ thể
> (guide §4.2), nhưng kit luôn có `pcbTechnology`. Đây cũng là field quyết định switch HE/Mechanical
> trong `IsCompatibleSwitch` của buyer hiện tại — nhất quán, không cần query thêm.
>
> **Fallback khi payload thiếu/hỏng:** nếu không parse được `kit` hoặc `requiredSwitchQuantity` (payload
> rỗng/cũ, vd các `DEMO_REQ_*` chỉ có `{"demo":true,...}`), QC **không** được crash — dùng mặc định
> `requiredSwitchQuantity = 68`, `switchTechnology = "Mechanical"` và ghi `StatusMessage` cảnh báo.

**Logic:**
1. Parse payload → số phím + switchTechnology. Sinh layout phím từ `requiredSwitchQuantity` (guide §10.2 — không cần layout thật, đủ số phím khớp kit).
2. App gọi `DeviceService.StartSessionAsync(...)` **trước**, lấy `sessionId`, rồi đưa `sessionId` cho simulator.
3. Mỗi phím: random signal/latency/noise theo phân phối guide §10.3–10.5 (`System.Random`), keyed theo switchTechnology + requirement mặc định; chèn outlier theo tỉ lệ để demo các loại fail. Emit `KeyTelemetry` (gắn `sessionId`).
4. Phát telemetry:
   - **Mặc định — MQTT:** `PublishKeyTestAsync` trả `true` → broker → subscriber → `DeviceService.RecordKeyResultAsync`.
   - **Fallback — in-process:** nếu publisher trả `false` hoặc MQTT bị tắt, simulator gọi thẳng
     `DeviceService.RecordKeyResultAsync`. Không được chỉ no-op.
5. Hết phím:
   - MQTT path: publish `session-summary`; subscriber gọi `CompleteSessionAsync(sessionId)` sau khi đủ key rows.
   - fallback path: simulator gọi thẳng `CompleteSessionAsync(sessionId)` sau khi record xong toàn bộ key rows.

---

## 8. Phase 6 — Wiring (`MainWindow.xaml.cs`)

Thêm vào khối manual-DI hiện có (sau `requestRepository`/`requestService`), giữ nguyên phong cách:

```csharp
var mqttSettings = new MqttSettings();
var realtimeService = new MqttRealtimeService(mqttSettings);

var deviceRepository = new SqlDeviceRepository(connectionFactory);
var deviceSessionRepository = new SqlDeviceTestSessionRepository(connectionFactory);
var deviceKeyResultRepository = new SqlDeviceKeyTestResultRepository(connectionFactory);

// Telemetry transport: MqttSettings hiện có Enabled/Host/Port, không có IsConfigured.
IDeviceTelemetryPublisher deviceTelemetryPublisher = mqttSettings.Enabled
    ? new MqttDeviceTelemetryPublisher(mqttSettings)
    : new NullDeviceTelemetryPublisher();

var deviceService = new DeviceService(
    deviceRepository,
    deviceSessionRepository,
    deviceKeyResultRepository);      // KHÔNG truyền realtimeService/IRealtimeNotifier và KHÔNG re-publish telemetry

var deviceSimulator = new DeviceSimulator(deviceService, deviceTelemetryPublisher);

// Bắt buộc cho MQTT path: nếu không start subscriber thì publish MQTT xong sẽ không có ai ghi DB.
IDeviceTelemetrySubscriber? deviceTelemetrySubscriber = mqttSettings.Enabled
    ? new MqttDeviceTelemetrySubscriber(mqttSettings, deviceService)
    : null;
_ = deviceTelemetrySubscriber?.StartAsync();
```

Truyền `deviceService` (+ `deviceSimulator`) vào `MainShellViewModel` → seller/buyer dashboard VM.
Nếu bật `MqttDeviceTelemetrySubscriber`, dispose/stop nó ở `Closed` cạnh `realtimeService.DisposeAsync()`:

```csharp
Closed += async (_, _) =>
{
    if (deviceTelemetrySubscriber is not null)
    {
        await deviceTelemetrySubscriber.StopAsync();
    }

    await realtimeService.DisposeAsync();
};
```

---

## 9. Phase 7 — UI & Localization

### Seller (`ViewModels/SellerDashboardViewModel.cs` + `Views/SellerDashboardView.xaml`)

- Thêm `StartQcTestCommand = new AsyncRelayCommand(_ => StartQcTestAsync(), _ => CanStartQc())`, enable khi `SelectedRequest?.Status == RequestStatus.In_progress` (đặt cạnh `StartProgressCommand` đã có — seller VM **đã có** `SelectedRequest`).
- `StartQcTestAsync`: `StartSessionAsync` → chạy `deviceSimulator` cho `SelectedRequest` → reload kết quả vào VM.
- **QC summary card:** tested/passed/warning/failed, avg/max latency, avg/max noise, status.
- **Per-key DataGrid** mô phỏng `SellerRequestsGrid` sẵn có, cột theo guide §12:
  `Key · Press · Release · Press events · Bounce · Latency · Stuck · Result · Failure type · Reason`.
  Bind `ObservableCollection<DeviceKeyTestResult>`.

### Buyer (`ViewModels/BuyerDashboardViewModel.cs` + `Views/BuyerDashboardView.xaml`)

> **[FIX #9]** `BuyerDashboardViewModel` hiện có `ObservableCollection<BuildRequest> Requests` nhưng
> **không** có `SelectedRequest` (chỉ seller có). Để summary QC bind sạch, theo đúng pattern
> `SelectedSeller → LoadSellerStatsAsync` fire-and-forget đã có trong buyer VM:
>
> - **Thêm 2 property:**
>   - `public BuildRequest? SelectedRequest` — setter `SetProperty` rồi gọi `_ = LoadSelectedRequestQcAsync(value)`.
>   - `public DeviceTestSession? SelectedRequestQc { get; private set; }` + `SelectedRequestQcVisibility`
>     (`Visible` khi `!= null`, mirror `SellerStatsVisibility`).
> - **Load best-effort** (request có thể chưa từng QC):
>   `LoadSelectedRequestQcAsync(request)` → `request is null` ⇒ `SelectedRequestQc = null`; ngược lại
>   `DeviceService`/`IDeviceTestSessionRepository.GetLatestByRequestAsync(request.RequestId)`; bọc try/catch +
>   `AppLog.Error` như `LoadSellerStatsAsync`. Không có session ⇒ `SelectedRequestQc = null` ⇒ hiện
>   text "Chưa có dữ liệu QC" (`QcTest_NoData`).
> - **XAML:** grid `Requests` thêm `SelectedItem="{Binding SelectedRequest, Mode=TwoWay}"`; **panel tóm tắt**
>   bind theo `SelectedRequestQc` (guide §12: `65/68 keys passed`, avg latency, noise profile, status),
>   `Visibility="{Binding SelectedRequestQcVisibility}"`. **Không** lộ chi tiết per-key cho buyer.

### Converters & Localization

- `Converters/StatusConverters.cs` — mở rộng `StatusToBrushConverter`: `Pass`/`Passed` → `SuccessBrush`, `Warning` → `WarningBrush`, `Fail`/`Failed` → `DangerBrush`, `Running` → `InfoBrush`.
- `Localization/AppStrings.cs` — thêm cặp (vi,en) `QcTest_*` (vd `QcTest_Title`, `QcTest_Start`, `QcTest_Passed`, `QcTest_Failed`, `QcTest_PerKey`, `QcTest_Summary`, các header cột). Dùng `{loc:Tr QcTest_...}` trong XAML, `Tr(...)`/`TrFormat(...)` trong VM.

---

## 10. Verification

- **Build:** `dotnet build Custom_keyboard.csproj` sau mỗi phase (target `net10.0-windows`, WPF — cần host Windows/SDK).
- **DB:** chạy lại `Database/SqlServer/CreateSchema_Refactor.sql` trên `CustomKeyboard_Refactor`; xác nhận 3 bảng mới + FK (`FK_dts_*`, `FK_dktr_session/request/device`) + `CK_devices_type` có `QC_STATION` + index được tạo (idempotent, **không** seed cứng device). Trạm QC tạo on-demand khi seller chạy test (§3.3). Xem `memory/refactor-db-test-setup.md` cho cách chạy sqlcmd.
- **Logic (không cần UI):** theo harness fake-repository trong `Phase6Verification/Program.cs`:
  - `DeviceQcRules` với 6 ví dụ guide §6.1–6.6 (Pass, NoSignal, HighLatency, Chatter, StuckKey, WrongKey), assert đúng `KeyTestResult`/`KeyFailureType`.
  - **Vòng đời session:** `StartSessionAsync` → `RecordKeyResultAsync` nhiều lần → `CompleteSessionAsync`; assert không vỡ FK (fake repo kiểm tra session tồn tại trước khi nhận key result) và status tổng kết §7 (có Fail ⇒ Failed; chỉ Warning ⇒ Warning).
- **End-to-end:** chạy app, seller chọn request `In_progress`, bấm **Start QC Test** → grid từng phím + summary hiển thị và rows ghi vào `device_key_test_results`; buyer chọn request thấy bản tóm tắt. Đường MQTT verify khi có broker; không có broker thì publisher trả `false` và simulator fallback in-process để vẫn ghi DB.

### Build-order checklist

- [ ] Phase 1 — Schema (3 bảng + drop + index + QC_STATION + per-key FK/switch_technology; **không** seed cứng) ✅ DB tạo lại sạch
- [ ] Phase 2 — Models + Enums (+ `KeyTelemetry` DTO, `DeviceType.QC_STATION`) ✅ build
- [ ] Phase 3 — Repositories + `SqlTableNames` (session upsert) ✅ build
- [ ] Phase 4 — `DeviceQcRules` + `DeviceService` (Start/Record/Complete) ✅ unit test §6/§7 + vòng đời
- [ ] Phase 5 — Telemetry publisher/subscriber + `DeviceSimulator` (MQTT chính, in-process fallback) ✅ build
- [ ] Phase 6 — Wiring `MainWindow.xaml.cs` (mqttSettings, telemetry publisher + subscriber, không IRealtimeNotifier) ✅ app khởi động
- [ ] Phase 7 — UI seller (SelectedRequest có sẵn) + buyer (thêm SelectedRequest) + converter + localization ✅ end-to-end

---

## 11. Quyết định thiết kế

- **[FIX #1] MQTT là transport chính, in-process là fallback** — đúng yêu cầu đã chốt & guide §2/§9.0. SQL Server vẫn là source of truth; MQTT chỉ vận chuyển event, ghi DB là bắt buộc.
- **[FIX #2] Vòng đời session `Start → Record* → Complete`** — session row (Running) luôn tồn tại trước key result ⇒ không vỡ FK `session_id`, bất kể transport.
- **[FIX #3] Telemetry device dùng cặp `IDeviceTelemetryPublisher`/`IDeviceTelemetrySubscriber` riêng** — không nhồi vào `IRealtimeNotifier`.
- **[FIX #4] `device_key_test_results.switch_technology`** thêm vào để khớp model + guide §6 JSON.
- **[FIX #5] Simulator parse `RequestPayloadJson`** (`kit.requiredSwitchQuantity`, `kit.pcbTechnology`) — không cần repo/catalog.
- **[FIX #6] Buyer noise/latency requirement = OPTIONAL phase sau** — phase lõi dùng requirement mặc định `Normal`, noise không fail build.
- **[FIX #7] `DeviceType.QC_STATION`** cho trạm gộp; giữ 3 loại chuyên biệt cho báo cáo. Device row tạo **on-demand** (`GetOrCreateQcStationAsync`), không seed cứng trong schema (schema không seed users).
- **[FIX #8] Per-key FK tới `build_requests` & `devices`** + repository copy từ session ⇒ tránh lệch dữ liệu.
- **[FIX #9] Buyer thêm `SelectedRequest`** ⇒ summary QC bind sạch theo request đang chọn.
- **[FIX #10] Wiring dùng `MqttSettings.Enabled`, không dùng `IsConfigured`** vì class hiện tại không có property đó;
  subscriber MQTT được tạo/start thật khi MQTT bật.
- **[FIX #11] Publisher trả `false` để fallback direct-call**, không chỉ no-op; `DeviceService` không publish lại
  telemetry để tránh loop/duplicate.
- **[FIX #12] `CompleteSessionAsync` chỉ hoàn tất khi đủ key rows**; summary MQTT xử lý sau khi `testedKeys == totalKeys`
  hoặc retry.
- **[FIX #13] Buyer lấy QC bằng `GetLatestByRequestAsync`** để tránh mơ hồ khi một request có nhiều lần test.
- **[FIX #14] `failure_type` có CHECK constraint** để DB chỉ nhận các loại lỗi chuẩn hóa.
- **Tên cột FK bám schema thực** (`seller_user_id`, `request_id`, `device_id`) thay vì tên lỏng trong guide.

---

## 12. Change log so với bản đầu

| # | Vấn đề review | Mức | Đã xử lý ở |
| --- | --- | --- | --- |
| 1 | In-process đặt làm mặc định, mâu thuẫn yêu cầu MQTT | **Critical** | §1, §2 diagram, §7.1, §7.2, §11 |
| 2 | RecordKeyResult trước CompleteSession ⇒ vỡ FK session | **Critical** | §5, §6.2 (thêm `StartSessionAsync`), §10 |
| 3 | Truyền `IRealtimeNotifier` (không có method telemetry) | **Critical** | §6.3, §7.1, §8 |
| 4 | Model có `SwitchTechnology` nhưng DDL per-key thiếu cột | **Critical** | §3.2 (thêm cột) |
| 5 | Không nói rõ nguồn input simulator | Cao | §7.2 (parse `RequestPayloadJson`) |
| 6 | Buyer noise/latency requirement chưa tồn tại | Cao | §1, §3.5, §6.1 (default `Normal`) |
| 7 | DeviceType thiếu `QC_STATION` | Trung | §3.2 (CHECK), §3.3 (tạo on-demand, bỏ seed cứng), §4 |
| 8 | Per-key FK chưa chặt | Trung | §3.2 (thêm 2 FK), §3.3, §5, §6.2 (thứ tự FK) |
| 9 | Buyer chưa có `SelectedRequest` | Trung | §9 |
| 10 | Wiring dùng `mqttSettings.IsConfigured` không tồn tại | Cao | §8 (dùng `MqttSettings.Enabled`, tạo `mqttSettings` rõ ràng) |
| 11 | Subscriber MQTT còn optional/comment nên publish xong không ai ghi DB | Cao | §7.1, §8 (subscriber được start khi MQTT bật) |
| 12 | Fallback no-op có thể làm mất dữ liệu | Cao | §1, §7.1, §7.2 (publisher trả `false`, simulator direct-call service) |
| 13 | Nguy cơ publish-loop/duplicate telemetry | Cao | §2 diagram, §6.2, §6.3 (DeviceService persist-only, không re-publish) |
| 14 | Session summary có thể complete trước khi đủ key rows | Cao | §6.2, §7.1, §7.2 (chờ đủ `total_keys`/retry) |
| 15 | `GetByRequestAsync` mơ hồ nếu nhiều session/request | Trung | §5, §9 (`GetLatestByRequestAsync`) |
| 16 | `failure_type` chưa có CHECK constraint | Trung | §3.2 |
| 17 | Báo file plan bị mojibake | — | Người dùng xác nhận editor xem được; không xử lý trong lần sửa này. |
