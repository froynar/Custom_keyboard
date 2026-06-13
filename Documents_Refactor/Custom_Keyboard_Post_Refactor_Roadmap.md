# Custom Keyboard Builder — Đánh Giá Tiến Độ & Kế Hoạch Các Phase Tiếp Theo

**Ngày lập:** 12/06/2026
**Bối cảnh:** Trước refactor, **theo tài liệu/trạng thái ghi nhận** (`Documents/Custom_Keyboard_Project_Implementation_Plan.md`), dự án đã hoàn thành tới **Phase 5** (Buyer gửi request) trên mô hình cũ (layout + case/PCB/plate riêng) — mô tả này dựa trên kế hoạch cũ, **không phải đã được test lại** trên code hiện tại (code đã refactor). Sau đó dự án được **refactor sang mô hình kit-based** (ERD 18 bảng, `Documents_Refactor/`), thực hiện qua `REFACTOR_PLAN.md` Phase 0→6 và đã verify sạch (Phase6Verification 19/19, build 0 lỗi).

> **Nguồn sự thật hiện tại:** `Documents_Refactor/` + code nhánh `refactor/kit-based-erd`. Thư mục `Documents/` chỉ còn là tham khảo lịch sử của mô hình cũ.

---

## 1. Thay Đổi Mô Hình Cốt Lõi (so với kế hoạch gốc)

Kế hoạch gốc mô tả Buyer chọn **layout → case → PCB → plate → switch → keycap → stabilizer** và validate bằng `compatibility_rules`. Refactor đã thay bằng:

| Khái niệm cũ | Khái niệm mới (kit-based) |
|---|---|
| `cases` + `pcbs` + `plates` + 3 bảng layout-mapping | **1 bảng `keyboard_kits`** (gộp case/PCB/plate qua `included_parts` + `pcb_technology`/`switch_mount`/`required_switch_quantity`) |
| `compatibility_rules` | Validate trực tiếp trong `BuildService` (switch tech/mount khớp kit) |
| Cột linh kiện trực tiếp trên `builds` | `build_items` (mỗi dòng đúng 1 FK sản phẩm) |
| Cột switch-mod chi tiết (`lube_type`, `is_filmed`…) | `build_mods` (chỉ `mod_type`/`target_component`/`notes`) |
| (không có) | `accessories`, `chat_conversations`, `chat_messages` |

➡️ **Hệ quả:** mô tả Phase 4/5 gốc (configurator case/PCB/plate, snapshot case/pcb/plate) đã **bị thay thế** và **được làm lại** theo kit-based. Các tiêu chí "build phải có layout/case/PCB/plate" không còn áp dụng; thay bằng "build phải có kit + đủ switch + (khuyến nghị) keycap/stab".

---

## 2. Bảng Đánh Giá Tiến Độ (Phase gốc → trạng thái hiện tại)

| Phase gốc | Nội dung | Trạng thái | Ghi chú |
|---|---|---|---|
| **0** Foundation | env, schema, build | ✅ Done | `CreateSchema_Refactor.sql`, DB `CustomKeyboard_Refactor`, build xanh |
| **1** DB + seed + repo | schema/seed/repos | ✅ Done | 18 bảng, `SeedData_Refactor.sql`, repos kit-based + `IChatRepository` |
| **2** Auth + role | login/register/routing | ✅ Done | `AccountService` (PBKDF2), `MainShellViewModel` route theo role |
| **3** Admin foundation | user/seller/catalog/audit | ✅ Done | `AdminService` + `AdminDashboard` (5 loại catalog Kit/Switch/KeycapSet/Stabilizer/Accessory), audit log |
| **4** Buyer build config | configurator | ✅ Done *(đổi mô hình)* | Kit + build_items thay layout/case/pcb/plate; validation Error/Warning/Info + tổng giá realtime |
| **5** Buyer send request | request + snapshot | ✅ Done | `RequestService.SendRequestAsync`, snapshot kit+items, không lặp `buyer_id`, chặn request trùng active |
| **6** Seller process request | seller status flow | ✅ Done *(mới đạt sau refactor)* | `SellerDashboard` + `UpdateStatusAsync` state machine + quyền seller |
| **7** Audit/validation/hardening | làm chắc hệ thống | ✅ Done | Đã: audit log, role/active/verified checks, validate **format email/phone** khi đăng ký, **global handler UX-friendly + ghi log file** (`%LOCALAPPDATA%/CustomKeyboard/log.txt`, `Diagnostics/AppLog` + `ErrorReporter`), phân biệt **lỗi DB vs nghiệp vụ** trong `ExecuteSafeAsync`, invariant catalog (giá ≥ 0, kit switch qty) trong `Phase6Verification`. Còn lại (nice-to-have): polish thêm message admin |
| **8** MQTT realtime | optional | ✅ Done | `Realtime/` (`IRealtimeNotifier`/`IRealtimeSubscriber` + `MqttRealtimeService`, MQTTnet v4). Buyer publish "request mới" → seller reload; seller publish "status update" → buyer reload. DB-first, publish-after, best-effort (broker tắt → app vẫn chạy DB-only). Round-trip đã verify trên broker thật |
| **8A** SignalR chat realtime | chat realtime | 🟡 DB-only | Đã: `chat_conversations/messages`, `ChatService` (buyer-seller & admin-seller, chặn buyer-admin, participant check), `ChatView` nhúng 3 dashboard. **Thiếu: tầng SignalR realtime** (hiện phải refresh thủ công) |
| **9** Testing & demo | test matrix + UI polish | ✅ Done *(còn screenshot manual)* | Đã: `Phase6Verification` **18/18**, test matrix T01–T16, demo script 3 role, UI restyle/polish U0–U6 (DataGrid/badge/empty-loading state/analytics screenshot checklist). Còn lại: chụp screenshot GUI thủ công |
| **10** Packaging & handover | bàn giao | ✅ Done *(còn screenshot manual)* | Seed nhúng **hash thật `Password123`** (bỏ hash giả) + test tự động `seed accounts login`; `Phase10_Handover.md` (clean-machine step-by-step + troubleshooting + gói bàn giao); README có quickstart + bảng tài khoản seed. `Phase6Verification` **18/18**. Còn lại: chụp screenshot UI (manual GUI) |

**Tóm tắt:** Lõi nghiệp vụ MVP (Phase 0–6) **đã hoàn thành** trên mô hình kit-based. Phần còn lại là **hardening + realtime (tùy chọn) + hoàn thiện test/demo/bàn giao**.

---

## 3. Kế Hoạch Các Phase Tiếp Theo

> Thứ tự ưu tiên: **Phase 7 → Phase 9 → Phase 10** là đường tới "MVP lõi hoàn chỉnh, bàn giao được". **Phase 8 (MQTT)** và **tầng realtime của 8A (SignalR)** là tùy chọn, làm nếu còn thời gian / cần cho demo.

### Phase 7 — Hoàn Thiện Validation & Hardening

**Mục tiêu:** đóng các lỗ hổng validation/error còn lại để app khó vào trạng thái sai và không crash vì input.

- **Validation đăng ký/tài khoản** (`AccountService.RegisterBuyerAsync`):
  - Thêm check **format email** (regex cơ bản) và **phone** (độ dài/ký tự số).
  - Giữ nguyên check trùng username/email/phone (đã có).
- **Validation catalog/admin** (`AdminService`): rà lại thông báo lỗi cho giá âm, brand/layout không tồn tại (đa số đã có) — bổ sung message thân thiện.
- **Error handling/UX:**
  - Giữ global handler nhưng rút gọn dialog (hiện đang in full stack trace) → hiện message gọn + nút "chi tiết".
  - Phân biệt lỗi mất kết nối DB và lỗi nghiệp vụ trong các `ExecuteSafeAsync`.
- **Logging cơ bản:** ghi log lỗi ra file (vd `%LOCALAPPDATA%/CustomKeyboard/log.txt`) khi có exception nền/DB — phục vụ debug khi chấm điểm.
- **Bổ sung verifier:** thêm invariant cho catalog (giá ≥ 0, kit có layout tồn tại — một phần đã có ở DB-FK) vào `Phase6Verification`.

**Done khi:** input sai không làm crash; lỗi DB có thông báo dễ hiểu; đăng ký chặn email/phone sai format; có log file khi lỗi.

**Trạng thái (13/06/2026): ✅ đã triển khai** — build 0 lỗi, `Phase6Verification` 6/6:
- `AccountService.RegisterBuyerAsync`: regex format email + phone (chuẩn hóa bỏ khoảng trắng/dấu, 8–15 chữ số, cho phép `+`); giữ nguyên check trùng.
- `Diagnostics/AppLog.cs` (mới): ghi log ra `%LOCALAPPDATA%/CustomKeyboard/log.txt` (best-effort, không throw) + `ToUserMessage`/`IsDatabaseError` phân biệt lỗi data layer (`DbException`/`TimeoutException`) với lỗi nghiệp vụ.
- `Diagnostics/ErrorReporter.cs` (mới): global handler log + dialog gọn, nút "mở file log" thay vì in full stack trace.
- `App.xaml.cs` + `Commands/AsyncRelayCommand.cs`: dùng `ErrorReporter` (bỏ `ex.ToString()`).
- 4 `ExecuteSafeAsync` (Admin/Buyer/Seller/Chat): log lỗi + `StatusMessage = AppLog.ToUserMessage(ex)` (lỗi DB → thông báo thân thiện).
- `Phase6Verification/Program.cs`: thêm invariant "Catalog price non-negative" (5 bảng) + "Kit required switch quantity positive".

**File ảnh hưởng:** `Services/AccountService.cs`, `App.xaml.cs`, `Commands/AsyncRelayCommand.cs`, `ViewModels/{Admin,Buyer,Seller}DashboardViewModel.cs`, `ViewModels/ChatViewModel.cs`, `Phase6Verification/Program.cs`; mới: `Diagnostics/AppLog.cs`, `Diagnostics/ErrorReporter.cs`.

---

### Phase 8 — MQTT/Realtime Notification *(tùy chọn)*

**Mục tiêu:** đẩy realtime cho "request mới" và "đổi status" mà **không** thay vai trò source-of-truth của DB.

- Chọn broker (Mosquitto local / broker test).
- Topic: `keyboard/seller/{sellerUserId}/build-request/new`, `keyboard/build-request/{requestId}/status/update`.
- Buyer gửi request: **lưu DB trước → publish sau**. Seller nhận event → reload từ DB.
- Seller update status: **update DB trước → publish sau**.

**Done khi:** MQTT tắt thì request vẫn lưu DB; seller offline không mất request; realtime chỉ là lớp bổ sung.

**Rủi ro:** thêm dependency hạ tầng; cắt scope này trước nếu thiếu thời gian.

**Trạng thái (13/06/2026): ✅ đã triển khai** — build 0 lỗi, `Phase6Verification` 10/10, round-trip live PASS trên `broker.hivemq.com`:
- `Realtime/IRealtimeNotifier` (publish) + `Realtime/IRealtimeSubscriber` (subscribe) + `Realtime/NullRealtimeNotifier` (mặc định/test) + `Realtime/MqttSettings` (host/port/enabled/topic root).
- `Realtime/MqttRealtimeService` (MQTTnet v4.3.7): publish/subscribe đều **best-effort, chạy nền** (không block UI), lazy-connect + backoff 30s khi không có broker + auto-reconnect 5s khi rớt; publish topic đúng `keyboard/seller/{id}/build-request/new` và `keyboard/build-request/{id}/status/update`.
- `RequestService`: **lưu DB trước → publish sau** trong `SendRequestAsync` + `UpdateStatusAsync`; publish bọc `PublishSafelyAsync` (lỗi publish chỉ log, không ảnh hưởng kết quả DB).
- `MainShellViewModel`: `StartAsync` khi login (subscribe theo role), `StopAsync` khi logout; event realtime → reload dashboard qua Dispatcher (`SellerDashboardViewModel.ReloadRequestsAsync` / `BuyerDashboardViewModel.ReloadRequestsAsync`).
- `MainWindow`: 1 `MqttRealtimeService` dùng chung cho notifier + subscriber; dispose khi đóng cửa sổ.
- Test: `Phase6Verification` "RequestService publishes realtime after DB write (best-effort)" — publish đúng id/status sau khi lưu, và **notifier lỗi không phá DB write**.
- **Bật realtime:** chạy broker MQTT ở `localhost:1883` (vd Mosquitto) — không có broker thì app vẫn chạy DB-only. Đổi host/port qua `MqttSettings` trong `MainWindow`.
- **Còn lại (tùy chọn):** đưa `MqttSettings` ra file config ngoài thay vì hard-code default.

**File ảnh hưởng:** mới `Realtime/*`; sửa `Services/RequestService.cs`, `ViewModels/MainShellViewModel.cs`, `ViewModels/SellerDashboardViewModel.cs`, `ViewModels/BuyerDashboardViewModel.cs`, `MainWindow.xaml.cs`, `Custom_keyboard.csproj` (PackageReference MQTTnet), `Phase6Verification/Program.cs`.

---

### Phase 8A — Chat Realtime (SignalR) trên nền chat DB sẵn có

**Mục tiêu:** nâng chat hiện tại (DB-only) lên realtime; **không** làm unread/online/typing.

Hiện đã có sẵn (không phải làm lại): bảng `chat_conversations/messages`, `ChatService` (validate role + participant + chặn buyer-admin), `ChatView` nhúng 3 dashboard, gửi/đọc qua refresh.

- **SignalR host:** thêm ASP.NET Core SignalR hub (hoặc tiến trình host nhỏ); WPF client kết nối sau đăng nhập.
- **Luồng:** gửi tin → `ChatService.SendMessageAsync` (lưu DB) → publish event theo conversation → client mở conversation đó **append** tin mới (không tạo badge).
- **UI:** bỏ thao tác "Refresh" thủ công khi có realtime; thêm điểm vào "Nhắn tin" (context menu) trên seller picker (Buyer) và tab Sellers (Admin) thay cho dropdown hiện tại nếu muốn sát kế hoạch gốc.
- **Giữ ràng buộc:** message rỗng bị chặn; sender phải là participant; user inactive không gửi được (service đã validate — chỉ cần giữ).

**Done khi:** 2 bên online thấy tin realtime; mất kết nối SignalR không mất tin (DB vẫn đủ); chat không đụng status request.

**File ảnh hưởng:** thêm project SignalR host; `ViewModels/ChatViewModel.cs`, `Views/ChatView.xaml`, DI ở `MainWindow.xaml.cs`.

---

### Phase 9 — Kiểm Thử & Hoàn Thiện Demo

**Mục tiêu:** chứng minh các luồng chính chạy được + sẵn sàng bảo vệ/demo.

- **Ánh xạ test matrix T01–T16** (mục §14 kế hoạch gốc) vào trạng thái hiện tại:
  - Đã phủ tự động qua `Phase6Verification` (9/9): T01 đăng ký (validate email/phone + normalize) và ban (AdminService ban user + chặn non-admin), T04/T05 (build hợp lệ/sai compat), T06 (request tới seller verified — SQL integration), **T07 (`RequestService` chặn seller unverified — unit test riêng; verified vẫn pass)**, T08–T11 (seller status), **T12 (`AdminService` ghi audit khi ban / verify seller / sửa catalog)**, T13–T14 (chat lưu DB), T15 (chặn buyer-admin).
  - Cần phủ thêm: T02 đăng nhập (login đúng/sai mật khẩu) hiện mới có UI smoke, chưa có unit riêng; T16 (realtime SignalR) — chỉ khi làm 8A.
  - Lập bảng test matrix có cột Pass/Defer + cách chạy.
- **UI polish:** cân nhắc đổi các danh sách (user/component/build/request) sang `DataGrid` để dễ đọc; chuẩn hóa title/tab; thông báo thành công/thất bại đồng nhất.
- **Demo script:** kịch bản 3 role + screenshot.

**Done khi:** test matrix có kết quả rõ ràng; UI đủ chuyên nghiệp để demo; có ảnh minh họa.

**Trạng thái (13/06/2026): ✅ test + tài liệu + UI polish đã xong, còn screenshot manual:**
- `Phase6Verification` nâng từ 10 → **13/13 PASS** (build 0 lỗi). Thêm 3 unit đóng đúng các lỗ test matrix:
  - `AccountService login accepts valid and blocks wrong/banned` — **T01 login** (đúng → set session) + **T02** (user `is_active=0` → `InactiveUser`, mật khẩu sai → `InvalidCredentials`).
  - `RequestService scopes requests to the owning seller` — **T08** (chủ sở hữu thấy request) + **T09** (seller khác không thấy / không update được).
  - `ChatService supports admin-seller conversations` — **T14** (admin↔seller lưu DB + đọc lại lịch sử).
- **Test matrix chính thức T01–T16** với cột trạng thái + cách chạy: `Documents_Refactor/Phase9_Test_Matrix.md` (**14 đạt + 1 một phần (T03) + 1 defer (T16)**; T16 defer theo Phase 8A; T03 ẩn/khôi phục component chưa có unit, kiểm manual UI).
- **Demo script 3 role + screenshot checklist**: `Documents_Refactor/Phase9_Demo_Script.md` (dùng tài khoản seed; `buyer_inactive` cho T02, `seller_unverified` cho T07).
- **Còn lại:** chụp screenshot GUI thủ công theo checklist mới trong `Phase9_Demo_Script.md`; phần UI polish đã chuyển sang DataGrid/badge/empty-loading state qua frontend U0–U6.

**File ảnh hưởng:** `Phase6Verification/Program.cs`; mới: `Documents_Refactor/Phase9_Test_Matrix.md`, `Documents_Refactor/Phase9_Demo_Script.md`.

---

### Phase 10 — Đóng Gói & Bàn Giao

**Mục tiêu:** chạy lại được trên máy khác / khi chấm điểm.

- **Hướng dẫn setup clean-machine:** cài SQL Server/SSMS → chạy `CreateSchema_Refactor.sql` → `SeedData_Refactor.sql` (đã nhúng hash PBKDF2 thật của `Password123`) → trỏ `SqlServerSettings.Database` → `dotnet build` → `dotnet run`.
- **Tài khoản seed:** ghi rõ admin/buyer/seller + mật khẩu `Password123`; không cần bước reset mật khẩu riêng.
- Kiểm tra app chạy khi DB rỗng (chỉ schema) và khi DB đã seed.
- Đóng gói báo cáo + ảnh.

**Done khi:** người khác theo hướng dẫn chạy được end-to-end; có script DB + báo cáo.

**Trạng thái (13/06/2026): ✅ đã triển khai (còn screenshot manual):**
- **Gỡ blocker mật khẩu seed:** `SeedData_Refactor.sql` nay nhúng **hash PBKDF2 thật của `Password123`** cho cả 7 tài khoản (thay 7 placeholder `PBKDF2-DEMO-HASH-...`). MERGE idempotent → chạy lại cũng sửa DB còn hash cũ. Đã áp vào DB live.
- **Test tự động end-to-end:** `Phase6Verification` có seed-account login, `StatsService` role/active guard, buyer dashboard switch filtering, SQL analytics aggregates và invariant DB. Runner hiện **18/18 PASS** sau các review fixes.
- **Handover doc:** `Documents_Refactor/Phase10_Handover.md` — setup clean-machine từng bước (lưu ý CreateSchema chạy **không kèm `-d`** vì tự `CREATE DATABASE`), bảng tài khoản seed, hành vi DB rỗng vs seeded, gói bàn giao, troubleshooting.
- **README:** thêm section "Cai Dat Clean Machine & Tai Khoan Demo" (quickstart + bảng seed) + cập nhật mô tả verification (18 checks).
- **Còn lại:** chụp screenshot UI 3 role (manual GUI; checklist trong `Phase9_Demo_Script.md` — blocker mật khẩu đã gỡ nên giờ chụp được).

**File ảnh hưởng:** `Documents_Refactor/SeedData_Refactor.sql`, `Phase6Verification/Program.cs`, `README.md`; mới: `Documents_Refactor/Phase10_Handover.md`.

---

## 4. Ma Trận Ưu Tiên (nếu thiếu thời gian)

1. ~~**Phase 7** — hardening (email/phone format, logging, error UX).~~ ✅ **Done (13/06/2026).**
2. ~~**Phase 9** — test matrix + demo script (UI polish nhẹ).~~ ✅ **Done (13/06/2026)** — còn screenshot manual.
3. ~~**Phase 10** — đóng gói/bàn giao.~~ ✅ **Done (13/06/2026)** — seed hash thật + handover doc + 18/18; còn screenshot manual.
4. **Phase 8A realtime (SignalR)** — nâng chat lên realtime. *(tùy chọn)*
5. ~~**Phase 8 (MQTT)** — realtime notification.~~ ✅ **Done (13/06/2026).**

> Không cắt: DB/auth/build/request/seller-status/chat-DB/audit — đây là lõi nghiệp vụ đã đạt.

---

## 5. Definition Of Done (cập nhật cho mô hình kit-based)

MVP lõi xem là hoàn thành khi (đã đạt ✅ trừ mục Phase 7 còn lại):

- ✅ App WPF build & chạy; SQL Server có schema + seed.
- ✅ Buyer tạo build kit-based và gửi request (snapshot).
- ✅ Seller xem/cập nhật request theo state machine.
- ✅ Admin quản lý user/seller/catalog (5 loại) + audit log.
- ✅ Mật khẩu không lưu plain text (PBKDF2); user bị ban không đăng nhập.
- ✅ Chat buyer-seller & admin-seller (DB), chặn buyer-admin.
- ✅ (Phase 7) Validation đầy đủ (email/phone format) + xử lý lỗi không crash + logging ra file.
- ✅ (Phase 9) Test matrix T01–T16 (14 pass + 1 partial + 1 defer) + demo script; còn screenshot manual.
- ✅ (Phase 10) Seed hash thật `Password123` + handover clean-machine + verification 18/18; còn screenshot manual.

Tùy chọn: ✅ MQTT (Phase 8) đã xong; SignalR realtime (Phase 8A) còn lại — không bắt buộc cho MVP lõi.

---

## 6. Tham Chiếu

- Kế hoạch gốc: `Documents/Custom_Keyboard_Project_Implementation_Plan.md` (mô hình cũ, tham khảo lịch sử).
- Kế hoạch refactor: `REFACTOR_PLAN.md` (Phase 0–6, đã xong).
- Báo cáo verify: `Documents_Refactor/Phase6_Verification_Report.md`.
- Kiến trúc hiện tại: `Architecture.md`; tổng quan: `CUSTOM_KEYBOARD_PROJECT_OVERVIEW.md`.
- Logic validation: `Documents_Refactor/Keyboard_Build_Validation_Logic.md`.

---

## 7. Review Fixes & Hạn Chế Đã Biết (sau Phase 10, 13/06/2026)

Sau rà soát toàn dự án, đã sửa các lỗi logic/đồng bộ (runner nâng lên **18/18**):

- **Build → Requested khi gửi request:** `RequestService.SendRequestAsync` nay gọi `IBuildRepository.SetStatusAsync(buildId, Requested)` sau khi lưu request. Thêm invariant chiều ngược "active request ⟹ build Requested" (`VerifyRefactor.sql` Extra B2 + runner) và unit test; đã vá 1 dòng data live còn lệch.
- **Switch quantity = at-least:** chốt rule **≥** `required_switch_quantity` (đúng `BuildService` + `Keyboard_Build_Validation_Logic.md`). Sửa verifier SQL + invariant runner từ `<>` (exact) sang `<` (thiếu mới lỗi).
- **Archive ẩn khỏi list chính:** `GetByBuyerAsync` (SQL + fake) lọc `status <> 'Archived'`; thêm unit test. `ArchiveAsync` nay gọi chung `SetStatusAsync`.
- **(False positive)** Admin Switch CRUD: `ActuationForceG is <= 0` là relational pattern trên `int?` → **không** match null, nên null vẫn được phép (đúng contract); chỉ chặn giá trị non-null ≤ 0. Không sửa.

**Hạn chế đã biết (chưa làm — hardening tương lai):**

- **Revalidate role/active giữa session:** login chặn user inactive, nhưng `SaveBuildAsync`/`SendRequestAsync`/`UpdateStatusAsync`/`SendMessageAsync` tin `userId` từ session, **không** kiểm lại user còn active/đúng role. Nếu admin ban/đổi role giữa lúc session mở, user vẫn thao tác được tới khi logout. Hướng sửa nhất quán: inject `IUserRepository` (ChatService đã có) vào các service mutating + guard `EnsureActiveAsync(userId, role)` ở đầu mỗi thao tác. Để ngoài đợt này vì là cross-cutting, cần làm đồng bộ cả 3 service + DI + fakes.
