# Custom Keyboard Builder - Project Overview & Refactor Status

**Current update:** 13/06/2026  
**Current status:** Kit-based MVP done through Phase 10 (Phase 7 hardening, Phase 8 optional MQTT realtime, Phase 9 test matrix/demo, Phase 10 clean-machine handover with real `Password123` seed hashes). Service/integration verification passes, DB invariants are clean, and buyer/seller/admin UI smoke login has no `Loi (UI thread)` popup. See `Documents_Refactor/Custom_Keyboard_Post_Refactor_Roadmap.md`.

## Phase 6 Verification Status

Phase 6 is implemented in `Phase6Verification/`.

Commands:

```powershell
dotnet build
dotnet run --project Phase6Verification\Phase6Verification.csproj
```

Latest verified result:

- `dotnet build`: pass, 0 warning, 0 error.
- `Phase6Verification`: 15/15 checks pass (grew from the Phase 6 baseline of 10; +5 in Phase 9/10).
- Unit-style checks: `BuildService` (totals, compat, archived hidden from list), `RequestService` (state machine, build→Requested, seller scoping, unverified block, realtime best-effort), `ChatService`, `AccountService` (register validation + login T01/T02), `AdminService` audit.
- SQL integration: temporary build/request/chat with cleanup (`P6_` data) + seed accounts login `Password123`.
- DB invariants: total snapshot, switch quantity (at-least), exactly-one-FK, seller verified, conversation XOR, FK orphan, requested build/request (both directions), chat sender participant, password hash format.
- UI smoke: `buyer_refactor`, `seller_soigear`, `admin_refactor` login successfully without UI-thread error popups.

Note: the test DB currently has an extra manually-created build (`kkkkkk`), and users may grow through app registration, so live verification checks exact counts for static/catalog tables and minimum seed baselines for users plus mutable transaction tables.

Legacy status block below is kept for historical context from before the code refactor.

**Ngày cập nhật:** 11/06/2026  
**Trạng thái:** Đang chuẩn bị refactor - Tài liệu sẵn sàng, cần bắt đầu refactor code

---

## 📌 Tóm Tắt Dự Án

### Mục Đích Hệ Thống
Hệ thống **Custom Keyboard Builder** cho phép:
- **Buyer (Người mua):** Xây dựng bàn phím tùy chỉnh bằng cách chọn một keyboard kit làm nền tảng, sau đó thêm switch, keycap, stabilizer, phụ kiện tùy ý, rồi gửi request đến seller để xây dựng.
- **Seller (Người bán/Xây dựng):** Nhận request từ buyer, quản lý trạng thái xây dựng, giao tiếp qua chat.
- **Admin (Quản trị viên):** Quản lý user, xác minh seller, quản lý catalog, xem audit log.

### Kiến Trúc Dự Án
- **Backend:** C# WPF (MVVM pattern)
- **Database:** SQL Server
- **Tổ chức:** Models → Services → Repositories → ViewModels → Views

---

## 🔄 Tình Hình Refactor: Từ Cũ → Mới

### ❌ Mô Hình Cũ (Documents/)
Cấu trúc phức tạp, chi tiết quá mức:
- **Case, PCB, Plate:** Tách thành 3 bảng riêng biệt → Buyer phải chọn từng cái
- **Build:** Lưu trực tiếp `SwitchId`, `KeycapId`, `StabilizerId` trên bảng
- **Compatibility Rules:** Quá chi tiết (ANSI/ISO, spacebar width, shift height, etc.)
- **Seller Inventory:** Quản lý stock riêng cho mỗi seller (phức tạp không cần)
- **Switch Mods:** Lưu chi tiết (`lube_type`, `is_filmed`, `spring_weight_g`)
- **ERD:** ~30+ relationships, quá nhiều để bắt đầu code

### ✅ Mô Hình Mới (Documents_Refactor/)
Gọn gàng, thực tế, chuẩn bị refactor:

**Nguyên Tắc Chính:**
1. **Keyboard Kit là Nền Tảng:** Kit đã bao gồm case, PCB, plate, foam, cable → Buyer chỉ chọn 1 kit
2. **BuildItem Pattern:** Tách riêng bảng `build_items` để lưu switch/keycap/stabilizer/accessory
3. **Simplify Compatibility:** Chỉ check layout/form factor, không chi tiết ANSI/ISO
4. **No Inventory:** Seller nhận request, không quản lý stock riêng
5. **Snapshot Pricing:** Lưu giá tại thời điểm tạo request (ngăn thay đổi giá)

---

## 📊 ERD Refactor (Mô Hình Dữ Liệu Mới)

**Tổng số:** 18 bảng, 26 relationships (gọn gàng, có thể quản lý)

### Nhóm Bảng & Mục Đích

#### 🔐 Account Management (3 bảng)
| Bảng | Trường Chính | Mục Đích |
|------|-------------|---------|
| `roles` | id, role_name | Định nghĩa Buyer, Seller, Admin |
| `users` | id, username, email, phone, password_hash, is_active, role_id | Tài khoản người dùng |
| `seller_profiles` | id, user_id, shop_name, is_verified, verified_at | Hồ sơ seller & trạng thái xác minh |

#### 🛒 Catalog (7 bảng)
| Bảng | Chính | Mục Đích |
|------|-------|---------|
| `brands` | id, brand_name | Hãng sản xuất |
| `layouts` | id, form_factor (60/65/75/TKL/100) | Layout & form factor |
| `keyboard_kits` | id, layout_id, required_switch_quantity | Kit nền tảng (case + PCB + plate + phụ kiện) |
| `switches` | id, switch_technology (Mechanical/HE), mount_type | Switch theo công nghệ |
| `keycap_sets` | id, supported_form_factor | Bộ keycap theo form factor |
| `stabilizers` | id, supported_layouts | Gói stabilizer cơ bản |
| `accessories` | id, accessory_type (Lube/Film/Cable/Foam/Tool) | Phụ kiện bổ sung |

#### 🎨 Build (3 bảng)
| Bảng | Mục Đích |
|------|---------|
| `builds` | Build của buyer, chứa kit + snapshot giá tổng |
| `build_items` | Các item trong build (switch/keycap/stab/accessory), mỗi dòng chỉ set 1 FK sản phẩm |
| `build_mods` | Yêu cầu mod (lube, film, tune, calibration) |

#### 📤 Request (1 bảng)
| Bảng | Mục Đích |
|------|---------|
| `build_requests` | Request buyer gửi cho seller, lưu payload snapshot |

#### 💬 Chat (2 bảng)
| Bảng | Mục Đích |
|------|---------|
| `chat_conversations` | Cuộc hội thoại buyer-seller hoặc admin-seller |
| `chat_messages` | Tin nhắn trong conversation |

#### 📋 Admin (1 bảng)
| Bảng | Mục Đích |
|------|---------|
| `audit_log` | Lịch sử thay đổi quan trọng |

---

## ✅ Validation Logic Cơ Bản

Build cần pass các check sau mới có thể save/request:

1. **Thông tin cơ bản:** Buyer active, tên build hợp lệ, status hợp lệ
2. **Kit:** Tồn tại, available, có layout
3. **Switch:** Tồn tại, available, technology & mount type khớp kit
4. **Switch Quantity:** Tổng quantity phải bằng `keyboard_kits.required_switch_quantity`
5. **Keycap:** Tồn tại, available, form factor khớp layout
6. **Stabilizer:** Tồn tại, available, layout khớp
7. **Accessory:** Mỗi dòng `build_items` phải có exactly one FK sản phẩm
8. **Seller (nếu request):** Active, verified, không trùng buyer
9. **Giá:** Total = kit price + Σ(build_items.quantity × unit_price)
10. **Request Flow:** Status transition hợp lệ (Pending → Accepted → In_progress → Completed)

### Phân Loại Lỗi
- **Error:** Build không thể save/request (thiếu kit, switch không khớp, seller chưa xác minh)
- **Warning:** Build có thể save nhưng cần lưu ý (thêu switch, keycap có thể không optimal)
- **Info:** Thông tin tính toán (tổng tiền, số switch cần, kit bao gồm gì)

---

## 📋 Functional Hierarchy (FHD Refactor)

### 5 Nhóm Chức Năng Chính

```
Custom Keyboard Builder
├─ 1. Tài khoản (1.1-1.4)
│  ├─ 1.1 Đăng ký
│  ├─ 1.2 Đăng nhập
│  ├─ 1.3 Đăng xuất
│  └─ 1.4 Xem thông tin tài khoản
│
├─ 2. Buyer - Tạo Build từ Kit (2.1-2.12)
│  ├─ 2.1 Dashboard buyer
│  ├─ 2.2 Danh sách build
│  ├─ 2.3 Tạo build mới
│  ├─ 2.4 Chọn keyboard kit
│  ├─ 2.5 Thêm linh kiện (switch/keycap/stab/accessory)
│  ├─ 2.6 Xem cảnh báo tương thích
│  ├─ 2.7 Xem tổng giá
│  ├─ 2.8 Lưu build
│  ├─ 2.9 Chọn seller
│  ├─ 2.10 Gửi request
│  ├─ 2.11 Theo dõi request
│  └─ 2.12 Lưu trữ build (archive)
│
├─ 3. Seller - Xử Lý Request (3.1-3.7)
│  ├─ 3.1 Dashboard seller
│  ├─ 3.2 Danh sách request
│  ├─ 3.3 Chi tiết request
│  ├─ 3.4 Chấp nhận request
│  ├─ 3.5 Cập nhật đang xử lý
│  ├─ 3.6 Hoàn thành request
│  └─ 3.7 Hủy request
│
├─ 4. Admin - Quản Trị (4.1-4.5)
│  ├─ 4.1 Dashboard admin
│  ├─ 4.2 Quản lý user
│  ├─ 4.3 Quản lý seller profile
│  ├─ 4.4 Quản lý catalog
│  └─ 4.5 Xem audit log
│
└─ 5. Chat (5.1-5.4)
   ├─ 5.1 Buyer chat với seller
   ├─ 5.2 Seller chat với buyer
   ├─ 5.3 Seller chat với admin
   └─ 5.4 Admin chat với seller
```

---

## 👥 Use Cases Theo Role

### UC-01: Buyer Tạo Build & Gửi Request
**Luồng chính:**
1. Đăng ký/Đăng nhập
2. Xem dashboard & danh sách build
3. Tạo build mới → Chọn kit → Thêm switch/keycap/stab/accessory
4. Xem cảnh báo, tổng giá → Lưu build
5. Chọn seller → Gửi request
6. Theo dõi trạng thái & chat với seller

### UC-02: Seller Xử Lý Request
**Luồng chính:**
1. Đăng nhập → Dashboard seller
2. Xem danh sách request → Chi tiết request
3. Chấp nhận → Cập nhật đang xử lý → Hoàn thành/Hủy
4. Chat với buyer về request

### UC-03: Admin Quản Trị
**Luồng chính:**
1. Quản lý user (khóa/mở, thay đổi role)
2. Xác minh/Loại bỏ seller
3. Quản lý catalog (brand, layout, kit, switch, keycap, stab, accessory)
4. Xem audit log

---

## 📊 Dataset Refactor (Seed Data)

**Tổng:**
- **roles:** 3 (Buyer, Seller, Admin)
- **users:** 7 (1 admin, 2 buyer active, 1 buyer inactive, 2 seller verified, 1 seller unverified)
- **seller_profiles:** 3 (2 verified, 1 pending)
- **brands:** 13 (Keychron, Keyed, Akko, NK, Glorious, Gateron, Cherry, Durock, Stabilizer, etc.)
- **layouts:** 4 (65, 75, TKL, 100)
- **keyboard_kits:** 9 (Mechanical & HE, mix of available/unavailable)
- **switches:** 10 (MX 3-pin, MX 5-pin, HE, mix of available/unavailable)
- **keycap_sets:** 5 (Universal & layout-limited)
- **stabilizers:** 4 (Simplified packages per layout)
- **accessories:** 7 (Lube, film, cable, foam, tool)
- **builds:** 5 (Draft, Saved, Requested, Archived)
- **build_items:** 17 (switch/keycap/stab/accessory choices)
- **build_mods:** 5 (lube, tune, calibration, film)
- **build_requests:** 2 (Pending, Completed)
- **audit_log:** 3 (Seller verify, catalog seed, request status)
- **chat_conversations:** 2 (Buyer-seller, Admin-seller)
- **chat_messages:** 4 (Test messages)

**Kiểm tra tính toán giá:**
| Build | Snapshot | Tính toán | Kết quả |
|-------|----------|-----------|---------|
| BUILD_REF_NEO65_MECH | 225.30 | 140 + 70×0.34 + 35 + 18 + 8.5 | ✓ |
| BUILD_REF_BOOG75_HE | 384.00 | 220 + 85×1.00 + 45 + 16 + 18 | ✓ |
| BUILD_REF_QK65_THOCK | 368.60 | 274.4 + 70×0.66 + 25 + 18 + 5 | ✓ |
| BUILD_REF_AULA_DRAFT | 89.30 | 40 + 85×0.58 | ✓ |
| BUILD_REF_ARCHIVED_NEO80 | 349.40 | 250 + 90×0.46 + 35 + 16 + 7 | ✓ |

---

## 📁 Cấu Trúc Project

```
Custom_keyboard/
├─ Documents_Refactor/             # ⭐ Tài liệu CHÍNH THỨC (kit-based) — design + plan + phase docs
│  ├─ Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml  # legacy snapshot
│  ├─ Custom_Keyboard_FHD_Refactor.md
│  ├─ Custom_Keyboard_Use_Cases_Refactor.md
│  ├─ Custom_Keyboard_DFD_Context_Level0_Refactor.md / _Level1_ / _Level2_
│  ├─ Custom_Keyboard_Class_Diagram_Refactor.md
│  ├─ Custom_Keyboard_Activity_Diagrams_Refactor.md
│  ├─ Keyboard_Build_Validation_Logic.md
│  ├─ Custom_Keyboard_Project_Refactor_Summary.md / Post_Refactor_Roadmap.md
│  ├─ REFACTOR_PLAN.md / Phase8A_SignalR_Hotspot_Demo_Plan.md
│  ├─ Architecture.md / CUSTOM_KEYBOARD_PROJECT_OVERVIEW.md
│  ├─ Phase6_Verification_Report.md / Phase9_Test_Matrix.md / Phase9_Demo_Script.md / Phase10_Handover.md / UseCaseVerification_Matrix.md
│  └─ SeedData_Refactor.sql / SeedData_Refactor_Check.md
│
├─ Models/                         # Domain models (Accounts, Builds, Components, Chat, Admin, Enums)
├─ Services/                       # Business logic (+ Security/)
├─ Repositories/                   # Data access interfaces (+ SqlServer/)
├─ Data/SqlServer/                 # Connection factory + settings
├─ ViewModels/ Views/ Commands/ Behaviors/ Converters/ Localization/ Analytics/ Realtime/ Diagnostics/ Themes/
│
├─ Database/SqlServer/             # SQL scripts (kit-based)
│  ├─ CreateSchema_Refactor.sql
│  ├─ SeedDemoAnalytics_Refactor.sql
│  ├─ ApplySellerApplications.sql
│  ├─ VerifyRefactor.sql
│  └─ PHASE0_SETUP.md
│
├─ Phase6Verification/             # Service/SQL verification runner
├─ WpfUiVerification/              # WPF UI automation runner
│
└─ README.md                       # Architecture.md & CUSTOM_KEYBOARD_PROJECT_OVERVIEW.md nay nam trong Documents_Refactor/
```

---

## 🔄 So Sánh Mô Hình Cũ vs Mới

| Khía Cạnh | Mô Hình Cũ | Mô Hình Mới |
|-----------|----------|----------|
| **Build base** | Layout + Case + PCB + Plate riêng | **Keyboard Kit** (bao gồm tất cả) |
| **Components trên Build** | SwitchId, KeycapId, StabilizerId trực tiếp | **BuildItem pattern** (flexible, scalable) |
| **Quantity** | `switch_quantity` trên build | Quantity trong `build_items` |
| **Compatibility** | Chi tiết (ANSI/ISO, spacebar, shift size) | **Gọn** (form factor, layout) |
| **Stabilizer** | Tách chi tiết (6.25u, 7u, kich thước) | **Gói cơ bản** (per layout) |
| **Seller Inventory** | Quản lý stock riêng | **Không có** (request-based) |
| **Switch Mods** | Trường riêng (lube_type, is_filmed) | **Build_mods table** (ghi chú) |
| **Accessory** | Tách riêng `accessories` | Vẫn tồn tại nhưng qua `build_items` |
| **ERD Complexity** | ~30+ relationships | **30 relationships** |
| **Code Scope** | Quá lớn để bắt đầu | **Có thể quản lý** |

---

## 📚 Tài Liệu Refactor (Thứ Tự Đọc)

1. ✅ **Custom_Keyboard_Project_Refactor_Summary.md** - Tóm tắt toàn bộ (đã đọc)
2. ✅ **../Documents/Custom_Keyboard_ERD_Final.dbml** - Schema vật lý cuối cùng (21 bảng / 30 quan hệ)
3. ✅ **Keyboard_Build_Validation_Logic.md** - Validation rules (đã đọc)
4. ✅ **Custom_Keyboard_FHD_Refactor.md** - Functional hierarchy (đã đọc)
5. ✅ **Custom_Keyboard_Use_Cases_Refactor.md** - Use cases chi tiết (đã đọc)
6. ⏳ **Custom_Keyboard_DFD_Context_Level0_Refactor.md** - Data flow diagram
7. ⏳ **Custom_Keyboard_DFD_Level1_Refactor.md** - Detailed processes
8. ⏳ **Custom_Keyboard_DFD_Level2_Refactor.md** - Sub-processes
9. ✅ **Custom_Keyboard_Class_Diagram_Refactor.md** - Class structure (đã đọc)
10. ✅ **SeedData_Refactor.sql** - Test data
11. ✅ **SeedData_Refactor_Check.md** - Data validation checks

---

## 🚀 Lộ Trình Refactor Tiếp Theo

### Phase 1: SQL Schema & Database
1. [ ] Tạo file SQL migration từ ERD DBML mới
2. [ ] Verify schema: 18 bảng, 26 relationships
3. [ ] Tạo database test riêng
4. [ ] Chạy schema
5. [ ] Chạy SeedData_Refactor.sql
6. [ ] Validate dữ liệu (queries check)

### Phase 2: Domain Models
1. [ ] Tạo classes mới theo ERD (18 bảng)
2. [ ] Xóa classes cũ không dùng (Case, PCB, Plate, old BuildItem columns)
3. [ ] Thêm BuildItem class
4. [ ] Thêm validation attributes

### Phase 3: Data Access (Repository & DbContext)
1. [ ] Tạo DbContext mới với 18 DbSet
2. [ ] Tạo repositories cho mỗi entity
3. [ ] Implement interfaces (CRUD operations)

### Phase 4: Business Logic (Services)
1. [ ] **BuildService:** Tạo/sửa/xóa build, tính snapshot, validate compatibility
2. [ ] **RequestService:** Gửi request, update status, lưu payload
3. [ ] **CatalogService:** Quản lý brands, layouts, kits, switches, keycaps, stabs, accessories
4. [ ] **ChatService:** Tạo conversation, gửi message
5. [ ] **AdminService:** Quản lý users, sellers, audit log

### Phase 5: ViewModels & UI
1. [ ] Buyer dashboard & build configurator
2. [ ] Seller request dashboard
3. [ ] Admin management panels
4. [ ] Chat UI

### Phase 6: Testing & Validation
1. [ ] Unit tests cho services
2. [ ] Integration tests với database
3. [ ] Test validation logic
4. [ ] Test dataset

---

## 🎯 Nguyên Tắc Refactor (Quan Trọng!)

✅ **Làm Đúng:**
- ERD mới là nguồn chính
- Không thêm relationship mới nếu không thực sự cần
- Giữ validation logic gọn (không chi tiết quá)
- Lưu snapshot giá tại thời điểm request
- Chỉ check layout/form factor, không chi tiết ANSI/ISO

❌ **Tránh:**
- Đưa lại `seller_inventory`, case/PCB/plate riêng
- Thêm switch_mod columns chi tiết (lube_type, spring_weight_g)
- Keycap/stabilizer compatibility quá chi tiết
- Tách BuildItem thành nhiều bảng khác nhau

⚠️ **Nếu Thay Đổi ERD:**
- Cập nhật FHD, Use Cases, DFD, Class Diagram
- Cập nhật seed dataset
- Cập nhật validation logic

---

## 📞 Liên Hệ & Tài Liệu

- **Người chịu trách nhiệm:** khoa đẹp trai vãi lồn
- **Email:** khoadurpypro@gmail.com
- **Folder tài liệu:** `/Documents_Refactor/`
- **Folder code hiện tại:** `/Models/`, `/Services/`, `/Repositories/`

---

## ✨ Tóm Tắt Tổng Quan

| Hạng Mục | Chi Tiết |
|---------|---------|
| **Mục tiêu** | Refactor từ mô hình phức tạp → mô hình gọn, kit-based |
| **Nền tảng chính** | Keyboard Kit (thay vì Case+PCB+Plate) |
| **Pattern chính** | BuildItem (thay vì trực tiếp trên Build) |
| **Database** | 18 bảng, 26 relationships (dễ quản lý) |
| **Validation** | 10 checks chính, phân chia Error/Warning/Info |
| **Tài liệu** | ERD, FHD, Use Cases, DFD, Class Diagram, Validation Logic sẵn sàng |
| **Dataset** | Seed data đầy đủ, tất cả 18 bảng, giá tính sẵn |
| **Bước tiếp theo** | Tạo SQL migration → Test database → Refactor code từ Models |

---

**Status:** ✅ Tài liệu refactor hoàn chỉnh, sẵn sàng bắt đầu refactor code
