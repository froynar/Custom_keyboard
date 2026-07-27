# Custom Keyboard Builder - Sequence Diagrams Simplified

Tai lieu nay mo ta bo **Sequence Diagram rut gon** cho toan bo du an
**Custom Keyboard Builder**.

Ly do khong ve tat ca vao mot diagram duy nhat:

- Du an co nhieu role: `Buyer`, `Seller`, `Admin`.
- Moi role co nhieu use case rieng: account, build/request, QC, chat, admin catalog, seller application, analytics.
- Neu gom het vao mot sequence diagram, so do se qua dai va kho doc.

Vi vay tai lieu nay tach theo cac luong nghiep vu chinh. Cac diagram van lien ket voi nhau va phu hop voi implementation hien tai.

---

## How To Use This Document

- Khi can trinh bay nhanh: dung `SD-S00`.
- Khi thay hoi "sequence cua ca du an dau?": dung `SD-C00`.
- Khi thay hoi tung chuc nang: dung bang `Coverage Matrix` de chi den diagram tuong ung.
- Khi can giai thich chi tiet flow: dung cac diagram `SD-S01` den `SD-S10`.

---

## SD-S00: Overall Project Sequence

Diagram nay la ban **simplified** tong quan cua ca he thong.

```mermaid
sequenceDiagram
    actor Buyer
    actor Seller
    actor Admin
    participant App as Custom Keyboard App
    participant DB as SQL Server Database
    participant Device as Device / QC Layer

    Admin->>App: Quan ly user, seller, catalog
    App->>DB: Luu user/seller/catalog/audit
    DB-->>App: Admin data updated

    Buyer->>App: Dang ky / dang nhap
    App->>DB: Kiem tra tai khoan
    DB-->>App: User + role
    App-->>Buyer: Mo Buyer Dashboard

    Buyer->>App: Tao build va gui request
    App->>DB: Luu build, build items, request snapshot
    DB-->>App: Request pending
    App-->>Seller: Request xuat hien trong Seller Dashboard

    Seller->>App: Accept va start request
    App->>DB: Cap nhat request status
    DB-->>App: Status updated

    Seller->>App: Run QC
    App->>Device: Start QC session
    Device->>App: Gui ket qua tung key
    App->>DB: Luu QC session + key results
    DB-->>App: QC summary saved

    Buyer->>App: Xem request va QC summary
    App->>DB: Lay request + latest QC session
    DB-->>App: Request status + QC summary
    App-->>Buyer: Hien thi tien do va ket qua QC
```

---

## SD-C00: Complete Overall Project Sequence KHONG CAN VE

Diagram nay gom cac nhom chuc nang chinh cua du an trong mot sequence lon. Muc tieu la bao phu toan bo he thong o muc vua du de bao ve truoc cau hoi tong quan.

```mermaid
sequenceDiagram
    actor Buyer
    actor Seller
    actor Admin
    participant Shell as Main Shell / UI
    participant Account as AccountService
    participant BuyerVM as BuyerDashboardViewModel
    participant SellerVM as SellerDashboardViewModel
    participant AdminVM as AdminDashboardViewModel
    participant Build as BuildService
    participant Request as RequestService
    participant Catalog as ComponentCatalogService
    participant AdminSvc as AdminService
    participant ApplySvc as SellerApplicationService
    participant Chat as ChatService
    participant Stats as StatsService
    participant Device as DeviceSimulator
    participant MQTT as MQTT Transport
    participant DeviceSvc as DeviceService
    participant DB as SQL Server Database

    Buyer->>Shell: Register/Login
    Shell->>Account: RegisterBuyerAsync / LoginAsync
    Account->>DB: SELECT/INSERT users
    DB-->>Account: User + role
    Account-->>Shell: Auth result
    Shell-->>Buyer: Buyer Dashboard

    Admin->>Shell: Login
    Shell->>Account: LoginAsync
    Account->>DB: SELECT users + roles
    DB-->>Account: Admin user
    Shell-->>Admin: Admin Dashboard

    Admin->>AdminVM: Manage users/sellers/catalog/applications
    AdminVM->>AdminSvc: Admin commands
    AdminSvc->>DB: UPDATE users/seller_profiles/catalog
    AdminSvc->>DB: INSERT audit_log
    DB-->>AdminSvc: Admin changes saved
    AdminSvc-->>AdminVM: Updated admin data

    Buyer->>BuyerVM: Load catalog and create build
    BuyerVM->>Catalog: Get available kits/components
    Catalog->>DB: SELECT catalog WHERE is_available = 1
    DB-->>Catalog: Available catalog
    BuyerVM->>Build: SaveBuildAsync
    Build->>Catalog: Validate selected kit/components
    Catalog->>DB: SELECT selected catalog rows
    DB-->>Catalog: Component details
    Build->>DB: INSERT/UPDATE builds, build_items, build_mods
    DB-->>Build: Saved build + snapshots
    Build-->>BuyerVM: Saved build

    Buyer->>BuyerVM: Send request to verified seller
    BuyerVM->>Request: SendRequestAsync
    Request->>Build: ValidateBuildAsync
    Build-->>Request: Valid build + total cost
    Request->>DB: Check seller verified/active
    Request->>DB: INSERT build_requests snapshot
    Request->>DB: UPDATE build status Requested
    DB-->>Request: Request saved
    Request-->>BuyerVM: Pending request

    Seller->>Shell: Login
    Shell->>Account: LoginAsync
    Account->>DB: SELECT users + roles
    DB-->>Account: Seller user
    Shell-->>Seller: Seller Dashboard

    Seller->>SellerVM: View request details
    SellerVM->>DB: SELECT seller requests
    DB-->>SellerVM: Request list + snapshots
    Seller->>SellerVM: Accept / Start / Cancel / Complete
    SellerVM->>Request: UpdateStatusAsync
    Request->>DB: UPDATE build_requests status
    DB-->>Request: Status saved
    Request-->>SellerVM: Updated request

    Seller->>SellerVM: Run QC for In_progress request
    SellerVM->>Device: RunQcTestAsync
    Device->>DeviceSvc: GetOrCreateQcStationAsync + StartSessionAsync
    DeviceSvc->>DB: INSERT/SELECT devices + INSERT device_test_sessions
    loop Each key
        Device->>Device: Generate key telemetry
        Device->>MQTT: Publish telemetry
        alt MQTT/subscriber persists telemetry
            MQTT->>DeviceSvc: RecordKeyResultAsync
        else MQTT unavailable or not persisted
            Device->>DeviceSvc: Fallback RecordKeyResultAsync
        end
        DeviceSvc->>DB: INSERT device_key_test_results
    end
    Device->>DeviceSvc: CompleteSessionAsync
    DeviceSvc->>DB: UPDATE device_test_sessions status + completed_at
    DeviceSvc-->>SellerVM: QC summary + key results

    Buyer->>BuyerVM: Track request and QC summary
    BuyerVM->>Request: GetBuyerRequestsAsync
    Request->>DB: SELECT buyer requests
    BuyerVM->>DeviceSvc: GetLatestSessionByRequestAsync
    DeviceSvc->>DB: SELECT views.Last_QC by request
    DB-->>BuyerVM: Request status + QC summary
    BuyerVM-->>Buyer: Show progress and QC result

    Buyer->>BuyerVM: Apply to become seller
    BuyerVM->>ApplySvc: SubmitAsync
    ApplySvc->>DB: INSERT seller_applications Pending
    Admin->>AdminVM: Approve/reject seller application
    AdminVM->>ApplySvc: ApproveAsync / RejectAsync
    ApplySvc->>DB: UPDATE application + role/profile + audit

    Buyer->>Shell: Chat / Profile / Logout / Language switch
    Shell->>Chat: Chat commands when chat is used
    Chat->>DB: SELECT/INSERT conversations/messages
    Shell->>DB: Read/write local preference where applicable

    Seller->>SellerVM: View analytics
    SellerVM->>Stats: GetSellerDashboardAsync
    Stats->>DB: Aggregate seller stats
    Admin->>AdminVM: View admin overview
    AdminVM->>Stats: GetAdminOverviewAsync
    Stats->>DB: Aggregate admin stats
```

---

## Coverage Matrix

Bang nay dung de tra loi nhanh: "chuc nang nay nam o sequence diagram nao?"

| FHD | Chuc nang | Diagram bao phu |
| --- | --- | --- |
| 1.1 | Dang ky | `SD-S01` |
| 1.2 | Dang nhap | `SD-S01` |
| 1.3 | Dang xuat | `SD-S09` |
| 1.4 | Xem profile tai khoan | `SD-S09` |
| 2.1 | Xem dashboard buyer | `SD-S02`, `SD-S08` |
| 2.2 | Xem danh sach build | `SD-S02` |
| 2.3 | Tao build moi | `SD-S02` |
| 2.4 | Chon keyboard kit | `SD-S02` |
| 2.5 | Them linh kien vao build | `SD-S02` |
| 2.6 | Xem canh bao tuong thich | `SD-S02` |
| 2.7 | Xem tong gia | `SD-S02` |
| 2.8 | Luu build | `SD-S02` |
| 2.9 | Chon seller | `SD-S02` |
| 2.10 | Gui request | `SD-S02` |
| 2.11 | Theo doi request/QC summary | `SD-S03`, `SD-S04` |
| 2.12 | Luu tru build | `SD-S02` |
| 2.13 | Dang ky tro thanh seller | `SD-S06` |
| 3.1 | Xem dashboard seller | `SD-S03`, `SD-S08` |
| 3.2 | Xem danh sach request | `SD-S03` |
| 3.3 | Xem chi tiet request | `SD-S03` |
| 3.4 | Chap nhan request | `SD-S03` |
| 3.5 | Cap nhat dang xu ly | `SD-S03` |
| 3.6 | Hoan thanh request | `SD-S03` |
| 3.7 | Huy request | `SD-S03` |
| 3.8 | Bat dau QC test | `SD-S03`, `SD-S04` |
| 3.9 | Xem ket qua QC tung phim | `SD-S03`, `SD-S04` |
| 3.10 | Xac nhan hoan thanh sau QC | `SD-S03` |
| 4.1 | Xem dashboard admin | `SD-S05`, `SD-S08` |
| 4.2 | Quan ly user | `SD-S05` |
| 4.3 | Quan ly seller profile | `SD-S05` |
| 4.4 | Quan ly catalog | `SD-S05`, `SD-S10` |
| 4.5 | Xem audit log | `SD-S05` |
| 4.6 | Duyet don xin lam seller | `SD-S06` |
| 5.1 | Buyer chat voi seller | `SD-S07` |
| 5.2 | Seller chat voi buyer | `SD-S07` |
| 5.3 | Seller chat voi admin | `SD-S07` |
| 5.4 | Admin chat voi seller | `SD-S07` |
| 6.1 | Tao/lay tram QC | `SD-S04` |
| 6.2 | Bat dau phien QC | `SD-S04` |
| 6.3 | Kiem tra tin hieu phim | `SD-S04` |
| 6.4 | Kiem tra latency | `SD-S04` |
| 6.5 | Kiem tra do on | `SD-S04` |
| 6.6 | Xem ket qua tung phim | `SD-S03`, `SD-S04` |
| 6.7 | Tong hop ket qua QC | `SD-S03`, `SD-S04` |

---

## SD-S01: Account And Role Routing

```mermaid
sequenceDiagram
    actor User
    participant App as Login/Register UI
    participant Account as AccountService
    participant UserRepo as SqlUserRepository
    participant DB as SQL Server Database

    User->>App: Dang ky hoac dang nhap
    App->>Account: RegisterBuyerAsync / LoginAsync
    Account->>UserRepo: Tim user va validate thong tin
    UserRepo->>DB: SELECT/INSERT users
    DB-->>UserRepo: User data
    UserRepo-->>Account: User + role

    alt Thong tin hop le
        Account-->>App: Success
        App-->>User: Dieu huong theo role Buyer/Seller/Admin
    else Thong tin khong hop le
        Account-->>App: Error
        App-->>User: Hien thi loi
    end
```

---

## SD-S02: Buyer Create Build And Send Request

```mermaid
sequenceDiagram
    actor Buyer
    participant App as Buyer Dashboard
    participant Catalog as ComponentCatalogService
    participant Build as BuildService
    participant Request as RequestService
    participant DB as SQL Server Database

    Buyer->>App: Mo man hinh tao build
    App->>Catalog: Load available catalog
    Catalog->>DB: SELECT kits/switches/keycaps/stabilizers/accessories WHERE is_available = 1
    DB-->>Catalog: Available components
    Catalog-->>App: Catalog data
    App-->>Buyer: Hien thi linh kien co the chon

    Buyer->>App: Chon kit + switch + keycap + stabilizer + accessory
    App->>Build: SaveBuildAsync(build)
    Build->>Catalog: Kiem tra ton tai, available, compatibility
    Catalog->>DB: SELECT selected components
    DB-->>Catalog: Component detail
    Catalog-->>Build: Component detail
    alt Build hop le
        Build->>DB: INSERT/UPDATE builds, build_items, build_mods
        DB-->>Build: Build saved
        Build-->>App: Build saved + total snapshot
        App-->>Buyer: Build da luu
    else Build sai compatibility/quantity/unavailable
        Build-->>App: Validation errors/warnings
        App-->>Buyer: Hien thi loi hoac canh bao
    end

    Buyer->>App: Chon seller va gui request
    App->>Request: SendRequestAsync(buildId, buyerId, sellerId)
    Request->>Build: ValidateBuildAsync
    Build-->>Request: Build valid + total cost
    Request->>DB: Check seller verified/active
    DB-->>Request: Seller valid
    Request->>DB: INSERT build_requests + request snapshot
    Request->>DB: UPDATE build status = Requested
    DB-->>Request: Request pending
    Request-->>App: Request created
    App-->>Buyer: Gui request thanh cong

    opt Buyer khong can build nua
        Buyer->>App: Archive build
        App->>Build: ArchiveBuildAsync
        Build->>DB: UPDATE build status = Archived
        DB-->>Build: Build archived
        App-->>Buyer: Build an khoi danh sach chinh
    end
```

---

## SD-S03: Seller Process Request And Run QC

```mermaid
sequenceDiagram
    actor Seller
    participant App as Seller Dashboard
    participant Request as RequestService
    participant Device as DeviceSimulator
    participant DeviceService
    participant DB as SQL Server Database

    Seller->>App: Mo danh sach request
    App->>DB: SELECT seller requests
    DB-->>App: Pending/Accepted/In_progress requests

    Seller->>App: Accept request
    App->>Request: UpdateStatusAsync(Accepted)
    Request->>DB: UPDATE build_requests
    DB-->>Request: Status updated
    Request-->>App: Accepted

    alt Seller tiep tuc xu ly
        Seller->>App: Start request
        App->>Request: UpdateStatusAsync(In_progress)
        Request->>DB: UPDATE build_requests
        DB-->>Request: Status updated
        Request-->>App: In_progress
    else Seller khong the xu ly
        Seller->>App: Cancel request
        App->>Request: UpdateStatusAsync(Cancelled)
        Request->>DB: UPDATE build_requests
        DB-->>Request: Cancelled
        Request-->>App: Request cancelled
    end

    Seller->>App: Run QC
    App->>Device: RunQcTestAsync(request)
    Device->>DeviceService: StartSessionAsync
    DeviceService->>DB: INSERT device_test_sessions

    loop Moi key tren keyboard
        Device->>Device: Sinh telemetry
        Device->>DeviceService: RecordKeyResultAsync
        DeviceService->>DB: INSERT device_key_test_results
    end

    Device->>DeviceService: CompleteSessionAsync
    DeviceService->>DB: UPDATE device_test_sessions status + completed_at
    DB-->>DeviceService: QC completed
    DeviceService-->>App: QC session summary
    App-->>Seller: Hien thi bang ket qua tung phim

    alt QC dat hoac warning chap nhan duoc
        Seller->>App: Complete request
        App->>Request: UpdateStatusAsync(Completed)
        Request->>DB: UPDATE build_requests completed_at
        DB-->>Request: Completed
        Request-->>App: Request completed
    else QC fail nghiem trong
        Seller->>App: Sua loi va chay QC lai
        App->>Device: RunQcTestAsync(request) again
    end
```

---

## SD-S04: Device QC With MQTT/Fallback

Diagram nay tach rieng vi day la phan device layer. MQTT la duong chinh, fallback ghi truc tiep khi broker/subscriber khong san sang.

```mermaid
sequenceDiagram
    participant Device as DeviceSimulator
    participant MQTT as MQTT Broker
    participant Subscriber as App MQTT Subscriber
    participant DeviceService
    participant DB as SQL Server Database

    Device->>DeviceService: StartSessionAsync
    DeviceService->>DB: INSERT device_test_sessions

    loop Moi key
        Device->>Device: GenerateKeyTelemetry
        Device->>MQTT: Publish key telemetry
        alt MQTT publish va subscriber OK
            MQTT->>Subscriber: Key telemetry message
            Subscriber->>DeviceService: RecordKeyResultAsync
            DeviceService->>DB: INSERT device_key_test_results
        else MQTT khong co broker hoac subscriber khong ghi DB
            Device->>DeviceService: Fallback RecordKeyResultAsync
            DeviceService->>DB: INSERT device_key_test_results
        end
    end

    Device->>DeviceService: CompleteSessionAsync
    DeviceService->>DB: Update tested/pass/warning/fail summary
    DB-->>DeviceService: Session completed
```

---

## SD-S05: Admin Management

Diagram nay gom user/seller/catalog/audit o muc rut gon.

```mermaid
sequenceDiagram
    actor Admin
    participant App as Admin Dashboard
    participant AdminService
    participant DB as SQL Server Database

    Admin->>App: Mo Admin Dashboard
    App->>AdminService: Load users/sellers/catalog/audit
    AdminService->>DB: SELECT admin data
    DB-->>AdminService: Dashboard data
    AdminService-->>App: Data loaded
    App-->>Admin: Hien thi admin tabs

    Admin->>App: Them hoac sua linh kien
    App->>AdminService: SaveComponentAsync
    AdminService->>DB: INSERT/UPDATE catalog table
    AdminService->>DB: INSERT audit_log
    DB-->>AdminService: Saved
    AdminService-->>App: Component saved
    App-->>Admin: Catalog updated

    Admin->>App: Hide/restore linh kien
    App->>AdminService: SetComponentAvailabilityAsync
    AdminService->>DB: UPDATE is_available
    AdminService->>DB: INSERT audit_log
    DB-->>AdminService: Availability updated
    AdminService-->>App: Done
```

---

## SD-S06: Buyer Apply To Become Seller

```mermaid
sequenceDiagram
    actor Buyer
    actor Admin
    participant App as Custom Keyboard App
    participant SellerApp as SellerApplicationService
    participant AdminService
    participant DB as SQL Server Database

    Buyer->>App: Nop don tro thanh seller
    App->>SellerApp: SubmitAsync
    SellerApp->>DB: INSERT seller_applications status Pending
    DB-->>SellerApp: Application saved
    SellerApp-->>App: Submitted
    App-->>Buyer: Don dang cho duyet

    Admin->>App: Xem danh sach don seller
    App->>DB: SELECT seller_applications
    DB-->>App: Pending applications

    Admin->>App: Approve application
    App->>SellerApp: ApproveAsync
    SellerApp->>DB: UPDATE application status Approved
    SellerApp->>DB: UPDATE user role = Seller
    SellerApp->>DB: INSERT/UPDATE seller_profile verified
    SellerApp->>DB: INSERT audit_log
    DB-->>SellerApp: Approved
    SellerApp-->>App: Buyer promoted to seller
```

---

## SD-S07: Chat

```mermaid
sequenceDiagram
    actor UserA as Buyer/Admin
    actor Seller
    participant App as Chat UI
    participant Chat as ChatService
    participant DB as SQL Server Database

    UserA->>App: Mo hoac tao conversation
    App->>Chat: GetOrCreateConversation
    Chat->>DB: SELECT/INSERT chat_conversations
    DB-->>Chat: Conversation
    Chat-->>App: Conversation opened

    UserA->>App: Gui message
    App->>Chat: SendMessageAsync
    Chat->>DB: Validate participant
    Chat->>DB: INSERT chat_messages
    DB-->>Chat: Message saved
    Chat-->>App: Message sent

    Seller->>App: Mo conversation
    App->>Chat: GetMessagesAsync
    Chat->>DB: SELECT chat_messages
    DB-->>Chat: Message history
    Chat-->>App: Chat history
    App-->>Seller: Hien thi tin nhan
```

---

## SD-S08: Analytics Dashboard

```mermaid
sequenceDiagram
    actor Seller
    actor Admin
    participant App as Dashboard UI
    participant Stats as StatsService
    participant DB as SQL Server Database

    Seller->>App: Mo Seller Analytics
    App->>Stats: GetSellerDashboardAsync
    Stats->>DB: Tong hop request/status/revenue/customer
    DB-->>Stats: Seller stats
    Stats-->>App: Chart data
    App-->>Seller: Hien thi analytics

    Admin->>App: Mo Admin Overview
    App->>Stats: GetAdminOverviewAsync
    Stats->>DB: Tong hop users/sellers/builds/requests
    DB-->>Stats: Admin stats
    Stats-->>App: Overview data
    App-->>Admin: Hien thi overview
```

---

## SD-S09: Profile, Logout And Language Preference

Diagram nay bao phu FHD 1.3, 1.4 va tinh nang doi ngon ngu trong UI.

```mermaid
sequenceDiagram
    actor User as Buyer/Seller/Admin
    participant Shell as Main Shell / Dashboard
    participant Account as AccountService
    participant Loc as Localization / Preferences
    participant Disk as preferences.txt

    User->>Shell: Mo user menu
    Shell-->>User: Hien thi username, email, phone, role, status, user id

    opt User doi ngon ngu
        User->>Shell: Chon Vietnamese/English
        Shell->>Loc: Set current culture
        Loc->>Disk: Save language preference
        Disk-->>Loc: Preference saved
        Loc-->>Shell: Notify UI text changed
        Shell-->>User: Dashboard cap nhat ngon ngu ngay
    end

    User->>Shell: Logout
    Shell->>Account: Logout
    Account-->>Shell: Clear current user
    Shell-->>User: Quay ve login screen
```

---

## SD-S10: Admin Catalog To Buyer Purchase Link

Diagram nay tra loi cau hoi quan trong: **Admin them linh kien xong Buyer co mua duoc khong?**

```mermaid
sequenceDiagram
    actor Admin
    actor Buyer
    actor Seller
    participant AdminUI as Admin Dashboard
    participant BuyerUI as Buyer Dashboard
    participant AdminSvc as AdminService
    participant Catalog as ComponentCatalogService
    participant Build as BuildService
    participant Request as RequestService
    participant DB as SQL Server Database

    Admin->>AdminUI: Tao brand/layout/kit/switch/keycap/stab/accessory
    AdminUI->>AdminSvc: SaveBrand/SaveLayout/SaveComponent
    AdminSvc->>DB: INSERT/UPDATE catalog tables
    AdminSvc->>DB: INSERT audit_log
    DB-->>AdminSvc: Catalog saved
    AdminSvc-->>AdminUI: Component available

    Buyer->>BuyerUI: Refresh catalog
    BuyerUI->>Catalog: GetAvailableKits/Components
    Catalog->>DB: SELECT WHERE is_available = 1
    DB-->>Catalog: Catalog includes admin-added components
    Catalog-->>BuyerUI: Available components
    BuyerUI-->>Buyer: Buyer thay linh kien moi

    Buyer->>BuyerUI: Tao build bang linh kien moi
    BuyerUI->>Build: SaveBuildAsync
    Build->>Catalog: Re-check existence, availability, compatibility
    Catalog->>DB: SELECT selected kit/components
    DB-->>Catalog: Component detail
    Catalog-->>Build: Valid catalog data
    Build->>DB: INSERT builds, build_items, build_mods
    DB-->>Build: Build saved
    Build-->>BuyerUI: Build saved

    Buyer->>BuyerUI: Gui request cho seller verified
    BuyerUI->>Request: SendRequestAsync
    Request->>DB: Validate seller active + verified
    Request->>DB: INSERT build_requests with snapshot
    DB-->>Request: Request pending
    Request-->>BuyerUI: Request sent
    BuyerUI-->>Seller: Seller nhan request

    opt Admin hide linh kien sau do
        Admin->>AdminUI: Hide switch/accessory/etc.
        AdminUI->>AdminSvc: SetComponentAvailabilityAsync(false)
        AdminSvc->>DB: UPDATE is_available = 0
        DB-->>AdminSvc: Hidden
        Buyer->>BuyerUI: Tao build moi bang linh kien da hidden
        BuyerUI->>Build: ValidateBuildAsync
        Build->>Catalog: Get selected component
        Catalog->>DB: SELECT component
        DB-->>Catalog: is_available = 0
        Catalog-->>Build: Component unavailable
        Build-->>BuyerUI: Validation error
        BuyerUI-->>Buyer: Khong the mua linh kien da hidden
    end
```

---

## Why SD-S02 And SD-S05 Are Separate

`Admin add component -> Buyer can purchase` duoc tach rieng trong `SD-S10` vi day la moi lien ket quan trong giua hai phan lon:

- Admin quan ly catalog.
- Buyer dung catalog do de tao build va gui request.

Neu gom vao diagram tong quan, chi thay "Admin quan ly catalog" va "Buyer tao build", nhung khong thay ro rang cau hoi:

> Admin them linh kien xong Buyer co that su thay va mua duoc khong?

Vi vay trong tai lieu nay:

- `SD-S00` cho cai nhin tong the ca du an.
- `SD-C00` cho sequence tong the complete hon.
- `SD-S02` mo ta Buyer build/request.
- `SD-S05` mo ta Admin management.
- `SD-S10` noi truc tiep Admin catalog -> Buyer purchase, dong thoi da duoc test bang Phase 6 verification.

---

## Sequence Diagram vs Activity Diagram

| Tieu chi | Sequence Diagram | Activity Diagram |
| --- | --- | --- |
| Muc dich | Mo ta ai goi ai theo thu tu thoi gian | Mo ta quy trinh nghiep vu va cac nhanh dieu kien |
| Tap trung vao | Actor/object/service/database giao tiep voi nhau | Cac buoc xu ly, decision, branch, loop |
| Cau hoi tra loi | "Buyer bam gui request thi service/DB nao duoc goi?" | "Neu build khong hop le thi quy trinh re nhanh the nao?" |
| Thanh phan chinh | Actor, participant, message, return, loop, alt | Action, decision, start/end, swimlane |
| Phu hop trong du an | Giai thich luong UI -> Service -> DB -> Device | Giai thich use case: tao build, gui request, QC pass/fail |

Noi ngan gon:

- **Sequence Diagram** la ban do giao tiep giua cac thanh phan trong he thong.
- **Activity Diagram** la ban do cac buoc nghiep vu va dieu kien re nhanh.
