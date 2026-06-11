# Custom Keyboard Builder - Ke Hoach Trien Khai Tong The

Tai lieu nay lap ke hoach trien khai du an Custom Keyboard Builder dua tren cac tai lieu da co:

- FHD: `Documents/Custom_Keyboard_FHD.md`
- DFD: `Documents/Custom_Keyboard_DFD_Context.md`, `Level0`, `Level1`, `Level2`
- ERD: `Documents/Custom_Keyboard_ERD.dbml`
- Class Diagram: `Documents/Custom_Keyboard_Class_Diagram.md`
- Cau truc code hien tai: WPF .NET, MVVM skeleton, SQL Server/SSMS script

Muc tieu cua tai lieu nay la chia phase thuc te de code, test va hoan thien san pham. Tai lieu nay chi la ke hoach, chua thuc hien them code nghiep vu.

## 1. Pham Vi MVP

MVP can hoan thanh cac luong chinh sau:

| Role | Pham vi MVP |
| --- | --- |
| Buyer | Dang ky, dang nhap, tao build keyboard, chon linh kien, xem tong gia, luu build, chon seller, gui request, theo doi trang thai request. Phase phu co chat voi seller. |
| Seller | Dang nhap, xem request duoc gan, xem chi tiet build snapshot, cap nhat trang thai request, danh dau hoan thanh. Phase phu co chat voi buyer/admin. |
| Admin | Dang nhap, quan ly user, ban/mo ban user, doi role, quan ly seller profile, verify/unverify seller, quan ly linh kien, xem audit log. Phase phu co chat voi seller. |

Ngoai pham vi MVP:

- Khong can mo phong ky thuat ban phim nang cao.
- Khong can checklist chi tiet tung cong doan lam ban phim.
- MQTT/realtime co the de sau MVP. Database van la source of truth.
- Chat realtime SignalR la phase phu, khong bat buoc cho MVP loi.
- Backend API rieng co the de sau neu du an hien tai chi la WPF desktop + SQL Server.

## 2. Dinh Huong Ky Thuat

| Hang muc | Lua chon de xuat |
| --- | --- |
| UI | WPF .NET theo MVVM. |
| Database | SQL Server, thao tac/kiem tra bang SSMS. |
| Data access | Repository layer dung query co tham so. Co the dung `Microsoft.Data.SqlClient` khi cai NuGet duoc. |
| Business logic | Tach vao Service: Account, Build, Request, Admin, Component Catalog. |
| State UI | ViewModel rieng cho Login, Buyer, Seller, Admin. |
| Bao mat | Password hash, khong luu plain text, user bi ban khong duoc dang nhap. |
| Audit | Ghi log cho thao tac admin va thay doi quan trong. |
| Realtime chat | Phase phu dung SignalR; DB van luu conversation/message truoc khi day realtime event. |

## 3. Thu Tu Trien Khai Tong Quan

Thu tu nen di nhu sau:

1. Nen tang SQL Server va cau truc project.
2. Auth va role.
3. Admin seed/quan ly danh muc co ban.
4. Buyer tao build.
5. Buyer gui request.
6. Seller xu ly request.
7. Audit, validation, hardening.
8. MQTT/realtime notification neu con thoi gian.
9. Phase phu chat SignalR neu can demo giao tiep buyer-seller/seller-admin.
10. Kiem thu, demo, bao cao. Test chat chi bat buoc neu Phase 8A duoc chon lam trong demo.

Ly do Admin/danh muc nen lam truoc Buyer: Buyer can layout, case, PCB, plate, switch, keycap, stabilizer va seller verified de tao build/gui request. Neu khong co seed data va trang thai seller, luong Buyer se kho test.

## 4. Phase 0 - Chuan Bi Moi Truong Va Nen Tang

### Muc tieu

Dam bao project build duoc, co cau truc thu muc ro rang, SQL Server san sang, SSMS co the tao va xem database.

### Cong viec

- Xac dinh SQL Server instance se dung:
  - `.\SQLEXPRESS`
  - `(localdb)\MSSQLLocalDB`
  - `localhost`
  - hoac instance khac tren may.
- Chay script `Database/SqlServer/CreateSchema.sql` trong SSMS.
- Kiem tra database `CustomKeyboardBuilder` co du bang theo ERD.
- Them file cau hinh connection string neu can, vi du `appsettings.local.json` hoac config rieng cho WPF.
- Chon cach data access:
  - Uu tien: `Microsoft.Data.SqlClient`.
  - Neu chua cai duoc NuGet, tam giu repository interface va cai package sau.
- Giu cau truc thu muc:
  - `Models`
  - `ViewModels`
  - `Views`
  - `Services`
  - `Repositories`
  - `Data/SqlServer`
  - `Database/SqlServer`

### Dau ra

- Project build thanh cong.
- Database tao duoc trong SQL Server.
- Co connection string dung voi may hien tai.
- Co checklist moi truong de nguoi khac chay lai.

### Tieu chi hoan thanh

- `dotnet build` khong loi.
- SSMS mo thay database va cac bang.
- Bang `roles` co role Buyer, Seller, Admin.

### Trang thai Phase 0 hien tai

- SQL Server instance dang dung: `KHOADZS1VN\SQLEXPRESS`.
- Database dang dung: `CustomKeyboardBuilder`.
- Script schema: `Database/SqlServer/CreateSchema.sql`.
- Script seed mau de kiem tra catalog: `Database/SqlServer/SeedSampleData.sql`.
- Script kiem tra Phase 0 doc-only: `Database/SqlServer/VerifyPhase0.sql`.
- Cau hinh ket noi mac dinh nam trong `Data/SqlServer/SqlServerSettings.cs`.
- Da xac nhan `dotnet build` khong loi.
- Da xac nhan database co du 19 bang theo ERD.
- Da xac nhan bang `roles` co du `Admin`, `Buyer`, `Seller`.

## 5. Phase 1 - Database, Seed Data Va Repository Nen

### Muc tieu

Bien ERD thanh nen tang du lieu co the doc/ghi that tu WPF.

### Cong viec

- Hoan thien script SQL:
  - Kiem tra khoa chinh, khoa ngoai.
  - Them index cho cac cot hay query: `user_id`, `buyer_id`, `seller_user_id`, `status`, `is_available`, `is_verified`.
  - Them constraint cho status neu can.
- Tao seed data ban dau:
  - Roles: Buyer, Seller, Admin.
  - 1 admin account mac dinh.
  - Brands mau.
  - Layouts mau: 60%, 65%, 75%, TKL.
  - Case/PCB/Plate/Switch/Keycap/Stabilizer mau.
  - Mapping layout: `case_layouts`, `pcb_layouts`, `plate_layouts`.
  - Mot vai compatibility rule co ban, gom rule PCB technology khop voi switch technology.
- Tao repository implementation cho cac bang chinh:
  - `UserRepository`
  - `SellerRepository`
  - `ComponentRepository`
  - `BuildRepository`
  - `RequestRepository`
  - `AuditLogRepository`
- Dung query co tham so, khong ghep chuoi SQL truc tiep tu input nguoi dung.

### Dau ra

- Database co du du lieu mau de test UI.
- Repository co the doc/ghi user, component, build, request.
- Co ham test connection SQL Server.

### Tieu chi hoan thanh

- Doc duoc danh sach layout/linh kien tu DB.
- Tao duoc user buyer moi.
- Tao duoc build mau.
- Tao duoc request mau.

## 6. Phase 2 - Auth, Role Va Dieu Huong Man Hinh

### Muc tieu

Nguoi dung dang nhap vao app va duoc dieu huong den dung role: Buyer, Seller, Admin.

### Cong viec

- Tao cac View:
  - `LoginView`
  - `RegisterView`
  - `ShellView` hoac `MainWindow` dieu huong role.
- Tao cac ViewModel:
  - `LoginViewModel`
  - `RegisterViewModel`
  - `MainShellViewModel`
- Tao `AccountService`:
  - Dang ky buyer.
  - Dang nhap bang email/username + password.
  - Kiem tra `is_active`.
  - Lay role user.
  - Dang xuat.
- Xu ly password:
  - Hash password truoc khi luu.
  - Khong hien password hash tren UI.
  - Khong cho admin xem mat khau that.
- Role routing:
  - Buyer vao dashboard buyer.
  - Seller vao dashboard seller.
  - Admin vao dashboard admin.

### Dau ra

- Man hinh login/register co the dung.
- Sau login, app hien dung dashboard theo role.
- User bi ban khong dang nhap duoc.

### Tieu chi hoan thanh

- Buyer moi dang ky va dang nhap duoc.
- Seller/Admin dang nhap duoc bang account seed/admin tao.
- Sai password bao loi ro rang.
- User inactive bi chan.

## 7. Phase 3 - Admin Foundation Va Quan Ly Du Lieu Nen

### Muc tieu

Admin quan ly du lieu can thiet de Buyer/Seller flow hoat dong.

### Cong viec

- Admin dashboard:
  - Tong so user.
  - Tong so seller.
  - Tong so linh kien.
  - Tong so request.
- User management:
  - Xem danh sach user.
  - Ban user: `is_active = false`.
  - Mo ban user: `is_active = true`.
  - Doi role user.
- Seller management:
  - Xem seller profile.
  - Tao/cap nhat seller profile.
  - Verify seller.
  - Unverify seller.
- Component management:
  - Xem danh sach component theo loai.
  - Them linh kien.
  - Sua thong tin linh kien.
  - An linh kien: `is_available = false`.
  - Khoi phuc linh kien: `is_available = true`.
- Audit:
  - Ghi log khi ban/mo ban user.
  - Ghi log khi doi role.
  - Ghi log khi verify/unverify seller.
  - Ghi log khi them/sua/an/khoi phuc linh kien.

### Dau ra

- Admin co the tao du lieu that de Buyer test.
- Seller verified xuat hien trong danh sach Buyer co the chon.
- Linh kien hidden khong xuat hien cho Buyer.

### Tieu chi hoan thanh

- Admin ban user, user do khong dang nhap duoc.
- Admin verify seller, seller do co the nhan request.
- Admin an linh kien, Buyer khong thay linh kien do.
- Audit log ghi duoc thao tac.

## 8. Phase 4 - Buyer Build Configuration

### Muc tieu

Buyer co the tao cau hinh keyboard build tu danh muc linh kien va luu build.

### Cong viec

- Buyer dashboard:
  - Man hinh chinh sau dang nhap hien 2 hanh dong lon: `Xem build` va `Tao build moi`.
  - Goc tren phai hien user menu voi ten nguoi dung; menu toi thieu co xem thong tin tai khoan va dang xuat.
  - Doi mat khau co the de optional/hardening neu con thoi gian.
  - Xem build da luu theo danh sach tu tren xuong duoi.
  - Chon mot build da luu de xem chi tiet.
  - Tu chi tiet build co the nap build vao configurator de tiep tuc chinh sua.
  - Xem request da gui.
  - Nut/hanh dong tao build moi khoi tao build rong.
- Build configurator:
  - Chon layout.
  - Sau khi co layout, load case theo layout.
  - Sau khi co layout, load PCB theo layout.
  - Sau khi co layout, load plate theo layout.
  - Load switch available.
  - Vung chon linh kien mac dinh hien tat ca linh kien kha dung sau khi co layout.
  - Thanh ngang loc nhom linh kien gom: All, Case, PCB, Plate, Switch, Mod.
  - Khi buyer bam Case/PCB/Plate/Switch/Mod, chi hien linh kien cua nhom do.
  - O search tren thanh chon linh kien loc nhanh theo ten/id tren tat ca nhom dang hien thi; vi du `Neo65` co the hien Neo65Case, Neo65PCB va Neo65Plate.
  - Mod co the hien trong thanh ngang sau khi cac linh kien chinh da co, hoac giu la khu vuc rieng neu can rut gon UI.
  - Chon keycap, stabilizer co the giu dang truong rieng trong Phase 4 neu chua dua vao thanh ngang.
  - Chon mod/phu kien co ban.
  - Nhap ghi chu build neu co.
- Compatibility:
  - Loc case/PCB/plate theo layout.
  - Kiem tra rule case + PCB + plate.
  - Kiem tra PCB technology voi switch technology, vi PCB HE/Topre/mechanical chi dung voi switch cung technology.
  - Kiem tra `pcb.switch_mount` phai khop voi `switch.mount_type`.
  - Hotswap chi la thuoc tinh hien thi cua PCB trong Phase 4; sau Phase 3 khong con rule `hotswap_required`.
  - Hien canh bao neu khong hop le.
- Price:
  - Tinh tong gia snapshot tu linh kien da chon.
  - Luu `total_cost_snapshot`.
- Save build:
  - Luu vao `builds`.
  - Luu mod vao `build_mods`.

### Dau ra

- Buyer tao build hop le.
- Buyer xem tong gia.
- Buyer luu build va xem lai build trong danh sach.
- Buyer mo build da luu, xem chi tiet va nap lai vao configurator de chinh sua tiep.
- Buyer tim nhanh linh kien theo ten/id trong man hinh chon linh kien.

### Tieu chi hoan thanh

- Build day du layout/case/PCB/plate/switch/keycap/stabilizer luu thanh cong.
- Build sai compatibility hien canh bao va khong cho luu build hop le de gui request sau nay.
- Tong gia snapshot khong doi khi gia linh kien thay doi sau do.
- Man hinh Buyer sau login co 2 lua chon lon: xem build va tao build moi.
- Thanh chon linh kien loc duoc theo All/Case/PCB/Plate/Switch/Mod va search theo ten/id.

## 9. Phase 5 - Buyer Gui Request Cho Seller

### Muc tieu

Buyer gui request tu build da luu trong Buyer dashboard Phase 4. Request duoc luu vao database voi snapshot de seller xu ly ma khong phu thuoc vao viec buyer co sua build sau do hay khong.

Phase 5 ke thua truc tiep UI Phase 4:

- Diem vao chinh la man hinh chi tiet build da luu, noi dang co hanh dong `Gui request (Phase 5)`.
- Buyer co the vao request flow tu `Xem build` -> chon build -> `Gui request`, hoac sau khi luu build moi thi mo lai build trong danh sach/chi tiet de gui.
- Phase 5 khong tao lai configurator va khong thay doi logic chon linh kien cua Phase 4; chi them seller directory, send request command va request tracking.
- Neu build dang o configurator chua duoc luu, buyer phai luu build truoc khi gui request.

### Cong viec

- Seller directory:
  - Load danh sach seller verified/active.
  - Hien shop name, phone, address.
  - Cho buyer chon seller trong panel/dialog gui request gan voi build detail.
- Service:
  - Implement `RequestService` cho buyer request flow, khong de `IRequestService` chi la interface.
  - Dung `IBuildRepository` de lay build da luu va dam bao build thuoc buyer dang gui.
  - Dung `ISellerRepository` de dam bao seller active/verified.
  - Dung `IRequestRepository` de luu request va tra lai request da tao.
  - Tao snapshot tu build da luu, gom layout, case, PCB, plate, switch, keycap, stabilizer, mod, notes va total cost snapshot.
- Send request:
  - Buyer chon build da luu tu danh sach build roi mo man hinh chi tiet build.
  - Buyer chon seller.
  - Buyer nhap note neu co.
  - Tao `request_payload_json` snapshot cua build tai thoi diem gui.
  - Luu vao `build_requests`.
  - Status ban dau: `Pending`.
  - Khong cap nhat lai request payload neu buyer sua build sau khi request da gui.
- Buyer request tracking:
  - Xem danh sach request da gui.
  - Xem status: Pending, Accepted, In_progress, Completed, Cancelled.
  - Sau khi gui thanh cong, refresh danh sach request va giu build trong danh sach build.
- UI/ViewModel:
  - Bat nut/hanh dong gui request trong man hinh chi tiet build da luu.
  - Them state/panel chon seller va note vao `BuyerDashboardViewModel` thay vi tao flow tach roi khoi Buyer dashboard.
  - Dung build dang duoc chon trong build detail lam build can gui.
  - Refresh danh sach request sau khi gui thanh cong.
  - Hien thong bao ro khi build chua hop le, seller khong kha dung hoac request gui that bai.

### Dau ra

- Buyer gui request thanh cong.
- Request hien trong danh sach Buyer.
- Request co the duoc Seller xem o phase sau.

### Tieu chi hoan thanh

- Khong gui duoc request neu build khong ton tai.
- Khong gui duoc request neu build khong thuoc buyer hien tai.
- Khong gui duoc request neu build chua co cau hinh du de seller xu ly.
- Khong gui duoc request neu seller chua verified hoac bi inactive.
- Request payload giu snapshot du lieu build.
- Sau khi gui request, buyer co the xem request trong man `Xem request da gui`.

## 10. Phase 6 - Seller Xu Ly Request

### Muc tieu

Seller xem request duoc gan tu Phase 5, xem chi tiet build snapshot va cap nhat trang thai request. Seller khong doc truc tiep build live de tranh bi anh huong neu buyer tiep tuc sua build sau khi da gui request.

Phase 6 ke thua truc tiep ket qua Phase 5:

- Danh sach request seller xem duoc lay tu `build_requests` theo `seller_user_id`.
- Chi tiet build hien cho seller uu tien doc tu `request_payload_json`.
- Trang thai seller cap nhat se quay lai man `Xem request da gui` cua buyer sau khi buyer reload/refresh.

### Cong viec

- Seller dashboard:
  - Xem danh sach request duoc gan.
  - Loc theo status neu can.
  - Hien buyer, thoi gian gui, status.
  - Mo request detail tu mot dong trong danh sach request.
- Service:
  - Mo rong/hoan thien `RequestService.GetSellerRequestsAsync`.
  - Implement `RequestService.UpdateStatusAsync` voi rule chuyen trang thai va quyen seller.
  - Chi cho seller cap nhat request co `seller_user_id` khop user dang dang nhap.
- Request detail:
  - Parse `request_payload_json` va hien theo format de doc, khong bat seller doc JSON thuan.
  - Hien layout, linh kien, mod, total cost.
  - Hien note cua buyer.
  - Hien request id, buyer, requested_at va status hien tai.
- Status update:
  - Pending -> Accepted/Cancelled.
  - Accepted -> In_progress/Cancelled.
  - In_progress -> Completed/Cancelled.
  - Completed/Cancelled la trang thai ket thuc.
- Cap nhat timestamp:
  - `accepted_at` khi Accepted.
  - `completed_at` khi Completed.
  - `updated_at` moi lan doi status.
- Bao ve du lieu:
  - Seller chi xem/cap nhat request gan cho minh.
  - Seller khong sua build goc cua buyer trong Phase 6.

### Dau ra

- Seller xu ly tron ven request.
- Buyer thay duoc status moi.

### Tieu chi hoan thanh

- Seller khong xem duoc request cua seller khac.
- Seller khong chuyen trang thai sai rule.
- Completed request co `completed_at`.
- Buyer tracking cap nhat status sau khi reload.

## 11. Phase 7 - Audit, Validation Va Hardening

### Muc tieu

Lam he thong on dinh hon, giam loi du lieu va tang kha nang demo/thuyet trinh.

### Cong viec

- Validation UI:
  - Email dung format.
  - Phone khong rong.
  - Username/email unique.
  - Gia linh kien khong am.
  - Build phai co layout.
- Validation service:
  - Kiem tra role truoc khi thuc hien thao tac.
  - Kiem tra `is_active`.
  - Kiem tra `is_available`.
  - Kiem tra seller verified.
- Error handling:
  - Loi SQL.
  - Loi duplicate key.
  - Loi connection.
  - Loi input thieu.
- Audit viewer:
  - Xem ai thay doi bang nao.
  - Hien old/new JSON neu co.
- Logging co ban:
  - Ghi log noi bo khi loi ket noi DB.

### Dau ra

- App xu ly loi co thong bao ro.
- Du lieu kho bi sai trang thai.
- Audit log huu ich cho Admin.

### Tieu chi hoan thanh

- Cac input sai khong lam app crash.
- Loi database co thong bao de hieu.
- Audit log ghi du cac thao tac admin quan trong.

## 12. Phase 8 - MQTT/Realtime Neu Con Thoi Gian

### Muc tieu

Them realtime delivery cho request moi va update status, nhung khong lam database mat vai tro source of truth.

### Cong viec

- Chon MQTT broker:
  - Local Mosquitto.
  - Broker test noi bo.
- Topic de xuat:
  - `keyboard/build-request/create`
  - `keyboard/seller/{sellerUserId}/build-request/new`
  - `keyboard/build-request/{requestId}/status/update`
- Khi Buyer gui request:
  - Luu DB truoc.
  - Publish event sau.
- Khi Seller dashboard dang mo:
  - Subscribe topic request moi.
  - Reload request tu DB khi nhan event.
- Khi Seller update status:
  - Update DB truoc.
  - Publish event sau.

### Dau ra

- Seller co the thay request moi nhanh hon.
- Buyer co the thay status moi sau khi reload hoac realtime.

### Tieu chi hoan thanh

- Neu MQTT tat, request van duoc luu DB.
- Seller offline khong mat request.
- Realtime chi la tang bo sung, khong thay DB.

## 13. Phase 8A - Phase Phu Chat Realtime SignalR

### Muc tieu

Them chat realtime giua Buyer-Seller va Seller-Admin. Khong co Buyer-Admin. Tin nhan phai luu DB truoc, SignalR chi dung de day event realtime khi nguoi nhan dang online. Phase nay khong lam unread count, online/offline indicator hoac typing indicator.

### Quan he voi cac phase khac

- Phase 0/1 la nen tang database/repository da co; khi lam Phase 8A thi tao migration/schema phu rieng cho chat, khong quay lai pha flow Phase 0/1 da chot:
  - `chat_conversations`
  - `chat_messages`
  - Seed/demo data neu can.
- Phase 2 can giu current user/role de kiem tra quyen chat.
- Phase 3 can them diem vao chat Admin-Seller trong man quan ly seller.
- Phase 5 can them diem vao chat Buyer-Seller:
  - Buyer bam chuot phai vao seller trong seller directory roi chon `Nhan tin`.
  - Buyer co the mo chat tu build detail/request detail neu da co seller.
- Phase 6 can them diem vao chat Seller-Buyer va Seller-Admin trong seller dashboard/request detail.
- Phase 8 MQTT notification request/status khong bi thay the; chat dung SignalR rieng, DB van la source of truth.

### Ket qua kiem tra frontend hien tai

| Role | Man hinh hien co | Dieu can chinh trong Phase 8A | Luu y tranh xung dot |
| --- | --- | --- | --- |
| Buyer | `BuyerDashboardView` da co seller picker trong luong gui request va danh sach request da gui. | Them context menu chuot phai/lenh `Nhan tin` tren seller picker, seller list hoac request/build detail co seller. | Khong doi logic gui request Phase 5; chat chi mo conversation rieng, khong tao/sua request. |
| Seller | `SellerDashboardView` hien moi la dashboard workflow co ban. | Phase 8A phai mo rong Seller dashboard/request detail de xem request, chon buyer trong request va mo chat; them diem chat voi admin. | Khong tron chat voi cap nhat status Phase 6; seller chi chat voi buyer cua request lien quan hoac admin. |
| Admin | `AdminDashboardView` da co tab Sellers va `SelectedSellerProfile`. | Them nut/context menu `Nhan tin` trong tab Sellers de admin chat voi seller dang chon. | Khong cho admin mo chat voi buyer tu tab Users; admin-seller chat khong thay the verify/unverify seller. |

### Ma tran dieu kien chat can kiem tra cheo

| Nguoi gui | Nguoi nhan | Cho phep? | Dieu kien bat buoc |
| --- | --- | --- | --- |
| Buyer | Seller | Co | Buyer dang active; Seller role Seller, active; seller duoc phep hien trong seller flow hoac co request/build lien quan. |
| Seller | Buyer | Co | Seller dang active; Buyer la buyer trong request/build lien quan neu chat mo tu request detail. |
| Seller | Admin | Co | Seller dang active; Admin role Admin, active. |
| Admin | Seller | Co | Admin dang active; Seller role Seller, active; co seller profile hop le. |
| Buyer | Admin | Khong | UI khong hien diem vao; service/DB constraint van phai chan neu bi goi truc tiep. |
| Admin | Buyer | Khong | UI khong hien diem vao; service/DB constraint van phai chan neu bi goi truc tiep. |
| Seller | Seller | Khong trong phase nay | Khong tao conversation seller-seller. |
| Buyer | Buyer | Khong trong phase nay | Khong tao conversation buyer-buyer. |
| Admin | Admin | Khong trong phase nay | Khong tao conversation admin-admin. |

### Cong viec

- Database/ERD toi thieu:
  - Tao `chat_conversations` voi `seller_user_id`, `buyer_id` nullable, `admin_user_id` nullable, `build_request_id` nullable, `created_at`, `updated_at`.
  - Tao `chat_messages` voi `conversation_id`, `sender_user_id`, `message_text`, `sent_at`.
  - Constraint nghiep vu: moi conversation phai co seller va dung mot trong hai dau con lai la buyer hoac admin.
  - Khong tao conversation Buyer-Admin.
- Service/repository:
  - `ChatRepository` de tao/lay conversation, luu tin nhan, lay lich su tin nhan.
  - `ChatService` de kiem tra role, kiem tra participant, chan Buyer-Admin, luu DB truoc va tra tin nhan vua luu.
- SignalR:
  - Them ASP.NET Core SignalR host hoac chat hub phu hop voi cach dong goi.
  - WPF client ket noi hub sau dang nhap.
  - Khi gui tin: luu DB truoc, sau do publish event theo conversation/user nhan.
  - Khi nhan event: UI reload/append tin nhan tu DB.
- UI/ViewModel:
  - Buyer:
    - Context menu chuot phai seller -> `Nhan tin`.
    - Chi hien/enable lenh chat khi co seller hop le.
    - Co the mo chat tu build detail/request detail neu ban ghi da co seller.
  - Seller:
    - Them UI danh sach request/detail neu Phase 6 chua day du o frontend.
    - Mo chat voi buyer tu request detail cua seller hien tai.
    - Mo chat voi admin tu dashboard/menu ho tro.
  - Admin:
    - Them nut/context menu `Nhan tin` trong tab Sellers.
    - Khong them chat buyer trong tab Users.
  - Man chat dung chung gom danh sach hoi thoai, lich su tin nhan, textbox nhap tin, nut gui.
  - Khi SignalR event den, UI append hoac reload conversation dang mo; khong tao badge unread.
  - Khong hien unread/online/offline/typing.
- Kiem tra dieu kien frontend/service:
  - UI khong hien nut chat sai role, nhung service van phai validate lai.
  - Message rong/chi khoang trang khong duoc gui.
  - Sender phai la participant cua conversation.
  - Conversation Buyer-Seller phai co `seller_user_id` va `buyer_id`, `admin_user_id` null.
  - Conversation Seller-Admin phai co `seller_user_id` va `admin_user_id`, `buyer_id` null.
  - User inactive/bi ban khong duoc mo chat hoac gui tin.
  - Chat khong tao audit/action request status moi trong Phase 5/6.

### Dau ra

- Buyer va Seller nhan tin duoc voi nhau.
- Seller va Admin nhan tin duoc voi nhau.
- Buyer va Admin khong tao/chat truc tiep duoc.
- Neu ca hai ben online, tin nhan cap nhat realtime qua SignalR.
- Neu mot ben offline, tin nhan van luu DB va thay khi mo app/chat lai.

### Tieu chi hoan thanh

- Tin nhan duoc luu trong DB truoc khi SignalR event phat ra.
- SignalR mat ket noi khong lam mat tin nhan.
- Khong co unread count, online/offline indicator, typing indicator.
- Chat khong lam thay doi build/request status.
- Chat khong pha flow Phase 5 gui request va Phase 6 xu ly request.

## 14. Phase 9 - Kiem Thu Va Hoan Thien Demo

### Muc tieu

Dam bao cac luong chinh chay duoc va san sang bao ve/demo.

### Test Case Chinh

| Ma | Test case |
| --- | --- |
| T01 | Buyer dang ky va dang nhap. |
| T02 | User bi ban khong dang nhap duoc. |
| T03 | Admin them/sua/an/khoi phuc linh kien. |
| T04 | Buyer tao build hop le va luu build. |
| T05 | Buyer tao build sai compatibility va thay canh bao. |
| T06 | Buyer gui request cho seller verified. |
| T07 | Buyer khong gui duoc request cho seller unverified. |
| T08 | Seller xem request duoc gan. |
| T09 | Seller khong xem duoc request cua seller khac. |
| T10 | Seller cap nhat status dung rule. |
| T11 | Seller khong cap nhat status sai rule. |
| T12 | Admin xem audit log. |
| T13 | Buyer chat voi seller, tin nhan luu DB va hien trong lich su. |
| T14 | Seller chat voi admin, tin nhan luu DB va hien trong lich su. |
| T15 | Buyer khong chat truc tiep Admin duoc. |
| T16 | Hai user dang online nhan tin moi qua SignalR event. |

Ghi chu: T13-T16 chi la test bat buoc khi Phase 8A duoc chon implement/demo. Neu giu MVP loi khong chat, cac test nay duoc danh dau deferred.

### Hoan thien UI

- Dat ten window/title ro rang.
- Chia tab/menu theo role.
- Dung DataGrid cho danh sach user, linh kien, build, request.
- Dung form rieng cho tao/sua.
- Co thong bao thanh cong/that bai.

### Hoan thien bao cao

- Cap nhat screenshot giao dien.
- Cap nhat script SQL.
- Cap nhat mo ta test case.
- Cap nhat ghi chu neu MQTT/SignalR chat chua lam trong MVP.

## 15. Phase 10 - Dong Goi Va Ban Giao

### Muc tieu

Lam du an de chay lai tren may khac hoac khi cham diem.

### Cong viec

- Viet huong dan chay:
  - Cai SQL Server/SSMS.
  - Chay `CreateSchema.sql`.
  - Cau hinh connection string.
  - `dotnet build`.
  - `dotnet run`.
- Them seed account:
  - Admin mac dinh.
  - Buyer test.
  - Seller test.
- Kiem tra clean build.
- Kiem tra app chay khi database rong va khi database da seed.
- Neu lam Phase 8A, ghi ro cach chay SignalR host va cau hinh endpoint cho WPF client.

### Dau ra

- Huong dan setup.
- Script database.
- App WPF chay du luong demo.
- Bao cao co anh minh hoa.

## 16. Moc Uu Tien Neu Thoi Gian Han Che

Neu thoi gian it, uu tien theo thu tu:

1. SQL schema + seed data.
2. Login/register + role routing.
3. Admin them/an linh kien va verify seller.
4. Buyer tao/luu build.
5. Buyer gui request.
6. Seller cap nhat status.
7. Audit log.
8. MQTT/realtime.
9. Chat SignalR Buyer-Seller/Seller-Admin.

Neu phai cat scope, cat MQTT/SignalR chat truoc. Khong nen cat database, auth, build, request, seller status vi day la loi nghiep vu cua du an.

## 17. Definition Of Done Cho MVP

MVP duoc xem la hoan thanh khi:

- App WPF build va chay duoc.
- SQL Server co schema va seed data.
- Buyer co the tao build va gui request.
- Seller co the xem/cap nhat request.
- Admin co the quan ly user/seller/linh kien.
- Status request tuan theo rule.
- Password khong luu plain text.
- Audit log ghi thao tac admin quan trong.
- Co test case demo duoc tren may.

Phase 8A chat khong bat buoc de dat MVP loi, nhung neu bat vao demo thi can dat cac tieu chi Phase 8A rieng.
