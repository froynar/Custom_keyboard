# Custom Keyboard Builder — Kế Hoạch Refactor Theo Phase

**Ngày tạo:** 11/06/2026
**Nguồn chuẩn:** `Documents_Refactor/` (ERD 18 bảng / 26 quan hệ)
**Phạm vi:** Refactor toàn bộ project từ mô hình "case/PCB/plate riêng" sang mô hình "kit-based + BuildItem".

> Nguyên tắc xuyên suốt: **ERD mới là nguồn sự thật duy nhất.** Không đưa lại `seller_inventory`, bảng case/PCB/plate riêng, compatibility chi tiết, hay cột switch-mod cũ (`lube_type`, `is_filmed`, `spring_weight_g`).

---

## 1. Hiện Trạng vs Mục Tiêu (Gap Analysis)

### 1.1 Database

| Khía cạnh | Hiện tại (`Database/SqlServer/CreateSchema.sql`) | Mục tiêu (ERD mới) |
|---|---|---|
| Bảng nền tảng build | `cases`, `pcbs`, `plates` + `case_layouts`, `pcb_layouts`, `plate_layouts` | `keyboard_kits` (1 bảng, gộp case/PCB/plate qua `included_parts`) |
| Compatibility | `compatibility_rules` | **Bỏ** — validate trực tiếp kit ↔ build item |
| Build components | Cột trực tiếp trên `builds` | `build_items` (mỗi dòng đúng 1 FK sản phẩm) |
| Accessory | Không có | `accessories` |
| Chat | Không có | `chat_conversations`, `chat_messages` |
| Tổng bảng | ~19 (gồm junction) | **18** |

**Bảng cần thêm:** `keyboard_kits`, `accessories`, `build_items`, `chat_conversations`, `chat_messages`.
**Bảng cần bỏ:** `cases`, `pcbs`, `plates`, `case_layouts`, `pcb_layouts`, `plate_layouts`, `compatibility_rules`.
**Bảng cần sửa cấu trúc:** `builds` (bỏ FK case/pcb/plate/switch/keycap/stab → thêm `kit_id`), `build_mods` (bỏ cột chi tiết), `keycap_sets`/`stabilizers`/`switches` (chuẩn lại cột theo ERD).

### 1.2 Domain Models (`Models/`)

| Hành động | Class |
|---|---|
| **Thêm mới** | `KeyboardKit`, `BuildItem`, `Accessory`, `ChatConversation`, `ChatMessage` |
| **Bỏ** | `KeyboardCase`, `Pcb`, `Plate`, `CompatibilityRule` |
| **Sửa** | `KeyboardBuild` (bỏ `CaseId/PcbId/PlateId/SwitchId/KeycapId/StabilizerId/LayoutId`; thêm `KitId`, `Items`); `BuildMod` (bỏ `LubeType/IsFilmed/SpringWeightG`); `BuildStatus` (thêm `Archived`); `AdminComponentType` (bỏ `Case/Pcb/Plate`; thêm `Kit/Accessory`) |
| **Giữ nguyên** | `Role`, `User`, `SellerProfile`, `Brand`, `Layout`, `KeyboardSwitch`, `KeycapSet`, `Stabilizer`, `BuildRequest`, `AuditLogEntry` (rà cột theo ERD) |

### 1.3 Data Access (`Data/`, `Repositories/`)

- `SqlTableNames.cs`: thêm hằng cho `keyboard_kits`, `accessories`, `build_items`, `build_mods`, `chat_conversations`, `chat_messages`; bỏ những bảng đã loại.
- `IComponentRepository` + `SqlComponentRepository`: bỏ `GetCases/Pcbs/Plates/CompatibilityRules`; thêm `GetKeyboardKits`, `GetAccessories`, `SaveKit`, `SaveAccessory`.
- `IBuildRepository` + `SqlBuildRepository`: load/save kèm `build_items` (transaction), thêm `ArchiveAsync`.
- **Thêm** `IChatRepository` + `SqlChatRepository`.

### 1.4 Services (`Services/`)

- `IComponentCatalogService`: bỏ cases/pcbs/plates/compatibility; thêm `GetAvailableKitsAsync`, `GetAvailableAccessoriesAsync`.
- `IBuildService` / `BuildService`: viết lại quanh kit + build_items; `CalculateTotalAsync` = `kit price + Σ(item.qty × unit_price)`; `ValidateBuildAsync` theo 10 check; thêm `ArchiveBuildAsync`.
- **Thêm** `IChatService` / `ChatService`.
- `IRequestService`: snapshot payload JSON; status flow Pending → Accepted → In_progress → Completed/Cancelled.
- `IAdminService`: cập nhật quản lý catalog mới (kit, accessory).

### 1.5 ViewModels & Views

- Thêm `BuildEditorViewModel` (thay/đổi từ `BuildModEditorViewModel`), `ChatViewModel`.
- Cập nhật `BuyerDashboardViewModel`, `SellerDashboardViewModel`, `AdminDashboardViewModel` theo mô hình kit/build_items.
- Thêm View Chat; cập nhật View buyer build configurator.

---

## 2. Lộ Trình Phased

> Thứ tự bắt buộc theo chiều phụ thuộc: **DB → Models → Repos → Services → ViewModels/UI → Test**. Mỗi phase phải build pass (`dotnet build`) và qua tiêu chí "Done" trước khi sang phase sau.

---

### Phase 0 — Chuẩn bị & an toàn

**Mục tiêu:** Bảo toàn trạng thái hiện tại, dựng nhánh & DB test.

- Tạo nhánh git `refactor/kit-based-erd`; commit mốc "pre-refactor".
- Backup file schema/seed cũ (đổi tên sang `*_Legacy.sql` hoặc đưa vào thư mục `Database/SqlServer/_legacy/`).
- Tạo database test riêng (vd `CustomKeyboard_Refactor`), không động vào DB runtime cũ.
- Xác nhận `dotnet build` hiện tại pass để có mốc so sánh.

**Done khi:** nhánh + backup sẵn sàng, DB test tạo được, build cũ pass.
**Rủi ro:** mất dữ liệu/scripts cũ → giảm thiểu bằng backup trước khi xóa.

---

### Phase 1 — SQL Schema & Seed (nền tảng dữ liệu)

**Mục tiêu:** Có schema mới 18 bảng + seed + query check pass. Đây là phase "khóa" thiết kế dữ liệu trước khi đụng code.

1. Viết `Database/SqlServer/CreateSchema_Refactor.sql` từ `Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml`, tạo bảng đúng thứ tự FK (roles → users → seller_profiles → brands → layouts → keyboard_kits → switches → keycap_sets → stabilizers → accessories → builds → build_items → build_mods → build_requests → audit_log → chat_conversations → chat_messages).
2. Thêm CHECK constraints quan trọng:
   - `build_items`: đúng 1 trong (`switch_id`, `keycap_id`, `stab_id`, `accessory_id`) khác NULL.
   - `chat_conversations`: đúng 1 trong (`buyer_id`, `admin_user_id`) khác NULL.
   - `quantity > 0`, giá `>= 0`.
3. Dùng `Documents_Refactor/SeedData_Refactor.sql` (đối chiếu `SeedData_Refactor_Check.md`).
4. Viết `Database/SqlServer/VerifyRefactor.sql` gồm các query check ở §14 của Refactor Summary (đếm dòng, FK orphan, total snapshot, switch quantity, exactly-one-FK, seller verified, conversation rule).
5. Chạy schema → seed → verify trên DB test.

**File ảnh hưởng:** `Database/SqlServer/CreateSchema_Refactor.sql` (mới), `VerifyRefactor.sql` (mới); scripts `ApplyPhaseX*.sql`/`CreateSchema.sql` cũ → chuyển legacy.
**Done khi:** seed chạy sạch, mọi query check trả 0 dòng lỗi, count = bảng kê trong Summary §12.1.
**Rủi ro:** sai thứ tự FK, CHECK constraint chặn seed hợp lệ → test trên DB riêng, không production.

---

### Phase 2 — Domain Models

**Mục tiêu:** Models C# khớp 1-1 với ERD mới; project vẫn build (tạm thời service/repo có thể stub).

1. Thêm: `Models/Components/KeyboardKit.cs`, `Models/Components/Accessory.cs`, `Models/Builds/BuildItem.cs` (+ helper `HasExactlyOneProduct()`), `Models/Chat/ChatConversation.cs` (+ `HasValidParticipants()`), `Models/Chat/ChatMessage.cs`.
2. Sửa: `KeyboardBuild` (bỏ Case/Pcb/Plate/Switch/Keycap/Stab/Layout Id → thêm `KitId`, `List<BuildItem> Items`); `BuildMod` (bỏ 3 cột chi tiết); `BuildStatus` thêm `Archived`; `AdminComponentType` (bỏ Case/Pcb/Plate, thêm Kit/Accessory).
3. Bỏ: `KeyboardCase.cs`, `Pcb.cs`, `Plate.cs`, `CompatibilityRule.cs` (và rà `KeyboardComponent.cs` nếu là base của chúng).

**File ảnh hưởng:** toàn bộ `Models/`.
**Done khi:** models phản chiếu đúng cột ERD; `dotnet build` chỉ còn lỗi ở repo/service (sẽ sửa phase sau) hoặc pass nếu stub.
**Rủi ro:** nhiều chỗ tham chiếu class bị xóa → dùng compiler errors làm checklist sang Phase 3-4.

---

### Phase 3 — Data Access (Repositories + DbContext mapping)

**Mục tiêu:** Repo đọc/ghi đúng schema mới.

1. `SqlTableNames.cs`: đồng bộ danh sách bảng mới.
2. `IComponentRepository`/`SqlComponentRepository`: bỏ case/pcb/plate/compat; thêm kit & accessory CRUD; cập nhật `AdminComponentRecord` mapping cho kit/accessory.
3. `IBuildRepository`/`SqlBuildRepository`: `GetByIdAsync`/`GetByBuyerAsync` load kèm `build_items` + `build_mods`; `SaveAsync` ghi build + items trong **1 transaction**; thêm `ArchiveAsync`.
4. Thêm `IChatRepository`/`SqlChatRepository`: `GetOrCreateConversationAsync`, `GetMessagesAsync`, `AddMessageAsync`.
5. Rà `SqlRepositoryHelpers.cs` cho mapper mới.

**File ảnh hưởng:** `Data/SqlServer/SqlTableNames.cs`, toàn bộ `Repositories/`.
**Done khi:** repo build pass; integration test cơ bản (đọc seed ra đúng số dòng) chạy được.
**Rủi ro:** transaction build+items lỗi nửa chừng → bọc transaction, rollback rõ ràng.

---

### Phase 4 — Business Logic (Services)

**Mục tiêu:** Service phản ánh đúng flow + validation mới.

1. `IComponentCatalogService`/`ComponentCatalogService`: API kits + accessories; bỏ cases/pcbs/plates/compat.
2. `BuildService`:
   - `CalculateTotalAsync` = `kit.price + Σ(item.qty × item.unitPriceSnapshot)`.
   - `ValidateBuildAsync` theo 10 check (`Keyboard_Build_Validation_Logic.md`): kit, switch tech/mount khớp kit, tổng switch qty = `required_switch_quantity`, keycap/stab form factor, accessory target, exactly-one-FK, seller verified, price snapshot.
   - Phân loại `Error` / `Warning` / `Info` trong `BuildValidationResult`.
   - `SaveBuildAsync`, `ArchiveBuildAsync`.
3. `RequestService`: `SendRequestAsync` (lưu `request_payload_json` snapshot), `UpdateStatusAsync` (state machine Pending→Accepted→In_progress→Completed/Cancelled, chặn transition sai).
4. Thêm `IChatService`/`ChatService`: tạo conversation (đúng quy tắc buyer XOR admin), gửi message, kiểm tra sender thuộc conversation.
5. `AdminService`: quản lý user/seller + catalog mới (kit, accessory), ghi `audit_log`.

**File ảnh hưởng:** toàn bộ `Services/`.
**Done khi:** services build pass; validation logic khớp tài liệu; unit test cho `ValidateBuildAsync`/`CalculateTotalAsync` pass.
**Rủi ro:** lệch công thức giá/quantity → viết unit test dựa trên 5 build mẫu (tổng đã biết: 225.30 / 384.00 / 368.60 / 89.30 / 349.40).

---

### Phase 5 — ViewModels & UI

**Mục tiêu:** UI đủ chức năng theo FHD (5 nhóm) & Use Cases.

1. `BuyerDashboardViewModel` + `BuildEditorViewModel`: chọn kit → thêm switch/keycap/stab/accessory (BuildItem) → xem cảnh báo/tổng giá → lưu/gửi request/archive.
2. `SellerDashboardViewModel`: list request, accept/in-progress/complete/cancel.
3. `AdminDashboardViewModel`: quản lý user/seller + catalog kit/switch/keycap/stab/accessory; xem audit log.
4. Thêm `ChatViewModel` + View Chat; nối vào buyer/seller/admin.
5. Cập nhật Views XAML tương ứng; cập nhật wiring trong `MainShellViewModel`/DI ở `App.xaml.cs`.

**File ảnh hưởng:** `ViewModels/`, `Views/`, `App.xaml.cs`, `MainWindow.xaml(.cs)`.
**Done khi:** app chạy được end-to-end các luồng UC-01/02/03 trên DB test.
**Rủi ro:** binding lỗi do đổi model → kiểm thử thủ công từng dashboard.

---

### Phase 6 — Testing & Validation (chốt)

**Mục tiêu:** Đảm bảo refactor đúng & không hồi quy.

1. Unit test: `BuildService` (validation + giá), `RequestService` (state machine), `ChatService` (quy tắc participant).
2. Integration test với DB test: CRUD build + items, gửi request, chat.
3. Chạy lại `VerifyRefactor.sql` sau khi app ghi dữ liệu.
4. Đối chiếu lại FHD/Use Cases: không thiếu màn hình/service nào.
5. Cập nhật `Architecture.md`, `README.md`, `CUSTOM_KEYBOARD_PROJECT_OVERVIEW.md` theo trạng thái mới; đưa `Documents/` cũ vào diện tham khảo.

**Done khi:** test pass, query check 0 lỗi, tài liệu cập nhật, merge nhánh `refactor/kit-based-erd`.

---

## 3. Ma Trận Phụ Thuộc Phase

```
Phase 0 ──> Phase 1 ──> Phase 2 ──> Phase 3 ──> Phase 4 ──> Phase 5 ──> Phase 6
(prep)     (SQL)       (models)    (repos)     (services)  (VM/UI)     (test)
```

Không nên đảo thứ tự: schema chốt trước để models bám theo; repos cần models; services cần repos; UI cần services.

## 4. Checklist Nguyên Tắc (chống lệch)

- [ ] Không thêm bảng/quan hệ ngoài 18 bảng / 26 quan hệ nếu chưa cập nhật ERD + FHD + Use Cases + DFD + Class Diagram + seed.
- [ ] Không tái xuất hiện `case/pcb/plate` riêng, `compatibility_rules`, `seller_inventory`, cột switch-mod chi tiết.
- [ ] Mỗi `build_items` đúng 1 FK sản phẩm; mỗi `chat_conversations` đúng 1 trong buyer/admin.
- [ ] Giá luôn là snapshot tại thời điểm tạo/gửi request.
- [ ] Mỗi phase: `dotnet build` pass + tiêu chí Done đạt trước khi sang phase kế.

## 5. Tài Liệu Tham Chiếu (theo thứ tự đọc)

1. `Documents_Refactor/Custom_Keyboard_Project_Refactor_Summary.md`
2. `Documents_Refactor/Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml`
3. `Documents_Refactor/Keyboard_Build_Validation_Logic.md`
4. `Documents_Refactor/Custom_Keyboard_FHD_Refactor.md`
5. `Documents_Refactor/Custom_Keyboard_Use_Cases_Refactor.md`
6. `Documents_Refactor/Custom_Keyboard_DFD_*` (Context/Level1/Level2)
7. `Documents_Refactor/Custom_Keyboard_Class_Diagram_Refactor.md`
8. `Documents_Refactor/SeedData_Refactor.sql` + `SeedData_Refactor_Check.md`

## Phase 6 Execution Update - 2026-06-12

Phase 6 da duoc thuc hien va verify bang:

```powershell
dotnet build
dotnet run --project Phase6Verification\Phase6Verification.csproj
```

Ket qua: build pass; `Phase6Verification` pass 6/6; UI smoke buyer/seller/admin pass, khong con popup `Loi (UI thread)`.

Chi tiet duoc ghi tai `Documents_Refactor/Phase6_Verification_Report.md`.
