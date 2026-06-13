# Custom Keyboard Builder

Phan mem WPF ho tro buyer tao cau hinh ban phim theo mo hinh kit-based, gui yeu cau build cho seller, chat theo role, va cho admin quan ly user/seller/catalog/audit log.

Du an hien tai la ung dung desktop WPF dung .NET va SQL Server database `CustomKeyboard_Refactor`.

## Trang Thai Refactor Hien Tai

- Refactor kit-based da hoan thanh loi MVP (Phase 0-10): build/request/seller-status/chat-DB/admin/audit + validation & logging (Phase 7) + MQTT realtime tuy chon (Phase 8) + test matrix & handover (Phase 9-10). Chat SignalR (Phase 8A) con lai, tuy chon.
- ERD refactor moi la source of truth: `Documents_Refactor/`.
- Tai lieu cu trong `Documents/` chi de tham khao lich su.
- Test/verification runner nam o `Phase6Verification/`.
- DB test mac dinh: `CustomKeyboard_Refactor` tren `KHOADZS1VN\SQLEXPRESS`.

Lenh kiem tra chinh:

```powershell
dotnet build
dotnet run --project Phase6Verification\Phase6Verification.csproj
```

## Cai Dat Clean Machine & Tai Khoan Demo

Huong dan setup day du (clean machine, troubleshooting, goi ban giao): **`Documents_Refactor/Phase10_Handover.md`**.

Quickstart:

1. Sua `Server` trong `Data/SqlServer/SqlServerSettings.cs` neu instance khac `KHOADZS1VN\SQLEXPRESS`.
2. Tao schema (script tu `CREATE DATABASE`, chay **khong kem `-d`**): `CreateSchema_Refactor.sql`.
3. Seed du lieu (da gom hash that cua `Password123`): `Documents_Refactor/SeedData_Refactor.sql`.
4. `dotnet build` -> `dotnet run`.
5. Verify: `dotnet run --project Phase6Verification\Phase6Verification.csproj` -> ky vong `Passed: 14`.

Tai khoan seed (mat khau **`Password123`**, login bang username hoac email):

| Username | Role | Ghi chu |
|---|---|---|
| `admin_refactor` | Admin | quan tri / audit |
| `buyer_refactor` | Buyer | build + request + chat mau |
| `buyer_second` | Buyer | buyer phu |
| `buyer_inactive` | Buyer | **banned** (`is_active=0`) — login bi chan |
| `seller_soigear` | Seller | **verified** |
| `seller_keyboardlab` | Seller | **verified** |
| `seller_unverified` | Seller | **chua verified** |

## Muc Tieu

He thong tap trung vao phan mem quan ly build keyboard, khong di qua sau vao mo phong ky thuat ban phim. MVP can lam duoc:

- Buyer dang ky, dang nhap, tao cau hinh keyboard custom tu keyboard kit.
- Buyer chon kit, switch, keycap, stabilizer, accessory va mod note co ban.
- Buyer gui yeu cau build cho seller.
- App luu build/request/chat vao SQL Server.
- Seller xem danh sach yeu cau build, nhan don, cap nhat trang thai hoan thanh.
- Admin xem thong tin user, ban/unban user, them linh kien, va an linh kien bang soft delete.

## Vai Tro

### Buyer

Buyer la nguoi tao build keyboard.

Chuc nang chinh:

- Quan ly tai khoan ca nhan.
- Xem danh sach linh kien kha dung.
- Tao va luu build.
- Xem tong gia snapshot cua build.
- Gui build request cho seller.
- Theo doi trang thai request.

### Seller

Seller la nguoi nhan va xu ly yeu cau build cua buyer.

Chuc nang chinh:

- Xem dashboard cac build request duoc gui den.
- Xem thoi gian request duoc tao.
- Xem chi tiet cau hinh build duoi dang JSON snapshot.
- Cap nhat trang thai request: Pending, Accepted, In_progress, Completed, Cancelled.
- Tick hoan thanh don bang cach doi status thanh Completed.

Seller checklist trong MVP chi can la trang thai hoan thanh don hang, khong can checklist chi tiet tung cong doan.

### Admin

Admin quan ly he thong.

Chuc nang chinh:

- Xem danh sach user, email, phone, role, trang thai active.
- Ban user bang `is_active = false`.
- Unban user bang `is_active = true`.
- Them linh kien moi.
- Sua thong tin linh kien.
- An linh kien bang `is_available = false`.
- Khoi phuc linh kien bang `is_available = true`.
- Xem audit log de biet ai da thay doi du lieu nao.

Admin khong duoc xem mat khau that cua user. Database chi luu `password_hash`. Neu can, admin chi nen reset password.

## Kien Truc Hien Tai

```text
WPF App
  -> MainWindow composition root
  -> MainShellViewModel
  -> Role dashboards
  -> Services
  -> SQL repositories
  -> SQL Server CustomKeyboard_Refactor
```

Database la source of truth. Realtime MQTT (Phase 8) la lop bo sung tuy chon: luu DB truoc, publish sau, best-effort — khong co broker thi app van chay DB-only. Chat realtime SignalR (Phase 8A) chua trien khai.

Neu seller offline, request van duoc luu trong database. Khi seller mo dashboard, app lay lai danh sach request tu SQL Server.

## Cong Nghe

- Desktop app: WPF, .NET
- Database: SQL Server
- Data access: `Microsoft.Data.SqlClient`
- Pattern: MVVM + repository/service layer
- Auth: username/email + password hash

## ERD Tom Tat

### User And Role

- `roles`: luu role buyer, seller, admin.
- `users`: tai khoan chung cho ca buyer, seller, admin.
- `seller_profiles`: thong tin rieng cua seller/shop.
- `audit_log`: lich su thao tac quan trong cua admin hoac user.

### Keyboard Components

- `brands`: thuong hieu linh kien.
- `layouts`: layout keyboard.
- `keyboard_kits`: kit nen tang gom case/PCB/plate/included parts.
- `switches`: switch.
- `keycap_sets`: bo keycap.
- `stabilizers`: stabilizer.
- `accessories`: phu kien bo sung.

Moi bang linh kien co `is_available` de ho tro soft delete.

### Compatibility

- Khong con bang `compatibility_rules`.
- App validate truc tiep trong `BuildService`.
- Kit quyet dinh `layout_id`, `pcb_technology`, `switch_mount`, va `required_switch_quantity`.
- Switch phai khop technology/mount voi kit.
- Keycap/stabilizer duoc check bang form factor/layout text.

### Build Flow

- `builds`: cau hinh build cua buyer.
- `build_items`: cac item switch/keycap/stabilizer/accessory; moi row dung exactly one product FK.
- `build_mods`: mod note co ban nhu lube, film, foam, calibration.
- `build_requests`: yeu cau build buyer gui cho seller.
- `chat_conversations`, `chat_messages`: chat buyer-seller va admin-seller.

`build_requests.status` la truong chinh de seller cap nhat tien do don hang.

## Build Request Status

De tranh du lieu lung tung, nen thong nhat status nhu sau:

```text
Pending      - buyer da gui yeu cau, seller chua nhan
Accepted     - seller da nhan yeu cau
In_progress  - seller dang xu ly
Completed    - seller da hoan thanh
Cancelled    - yeu cau bi huy
```

Khi seller tick hoan thanh:

```text
status = Completed
completed_at = current datetime
updated_at = current datetime
```

Khong can bang checklist rieng neu chi can biet don da hoan thanh hay chua.

## MQTT Topics De Xuat

Topic buyer gui request:

```text
keyboard/build-request/create
```

Topic backend forward request toi seller:

```text
keyboard/seller/{sellerUserId}/build-request/new
```

Topic seller update status:

```text
keyboard/build-request/{requestId}/status/update
```

Payload build request nen la JSON snapshot, vi gia va thong tin linh kien co the thay doi sau nay.

Vi du:

```json
{
  "requestId": "REQ001",
  "buildId": "BUILD001",
  "buyerId": 12,
  "sellerUserId": 5,
  "requestedAt": "2026-06-05T22:30:00",
  "build": {
    "layoutId": "75",
    "caseId": "CASE001",
    "pcbId": "PCB001",
    "plateId": "PLATE001",
    "switchId": "SW001",
    "keycapId": "KEYCAP001",
    "stabId": "STAB001",
    "totalCostSnapshot": 199.99
  }
}
```

## Luong Nghiep Vu

### Buyer Tao Build

1. Buyer chon layout.
2. App loc linh kien kha dung theo layout va compatibility.
3. Buyer chon case, PCB, plate, switch, keycap, stabilizer.
4. App tinh `total_cost_snapshot`.
5. Backend luu vao `builds`.

### Buyer Gui Request

1. Buyer bam gui yeu cau.
2. App dong goi build thanh JSON snapshot.
3. App publish MQTT message.
4. Backend nhan message.
5. Backend luu vao `build_requests`.
6. Backend publish message toi seller dashboard.

### Seller Xu Ly Request

1. Seller mo dashboard.
2. Dashboard load danh sach request tu database/backend.
3. Neu co request moi, MQTT day realtime vao dashboard.
4. Seller update status.
5. Khi hoan thanh, seller tick done.
6. Backend update `status`, `completed_at`, `updated_at`.

### Admin Quan Ly

1. Admin xem danh sach users.
2. Admin ban/unban user bang `users.is_active`.
3. Admin them/sua/an linh kien.
4. Backend ghi thay doi vao `audit_log`.

## Nguyen Tac Bao Mat

- Khong luu password plain text.
- Chi luu `password_hash`.
- Admin khong xem mat khau that.
- User bi ban khong duoc dang nhap.
- Seller chi nen xem request duoc gan cho minh.
- Buyer chi nen xem build va request cua minh.
- Admin moi co quyen them/sua/an linh kien.

## Ke Hoach Code De Xuat

### Phase 0: Chuan Bi Moi Truong Va Nen Tang

- Tao project WPF va cau truc thu muc.
- Kiem tra SQL Server/SSMS san sang.
- Xac nhan project build duoc.

### Phase 1: Database, Seed Data Va Repository Nen

- Tao database schema tu ERD.
- Tao model/entity tu cac bang chinh.
- Tao role buyer, seller, admin.
- Tao seed data cho brands, layouts, components.
- Tao repository nen cho user, component, build, request.

### Phase 2: Auth, Role Va Dieu Huong Man Hinh

- Man hinh login/register.
- Hash password va chan user inactive.
- Dieu huong Buyer/Seller/Admin dashboard theo role.

### Phase 3: Admin Foundation Va Quan Ly Du Lieu Nen

- User management.
- Seller profile va verify/unverify seller.
- Component management: them/sua/an/khoi phuc linh kien.
- Audit log viewer.

### Phase 4: Buyer Build Configuration

- Buyer dashboard hien build da luu va request da gui.
- Build configurator chon layout/case/PCB/plate/switch/keycap/stabilizer/mod.
- Kiem tra compatibility va tinh `total_cost_snapshot`.
- Luu build vao `builds` va mod vao `build_mods`.

### Phase 5: Buyer Gui Request Cho Seller

- Seller directory chi hien seller verified/active.
- Buyer chon build da luu, chon seller, nhap note request.
- Tao JSON snapshot va luu request `Pending`.
- Buyer theo doi request da gui.

### Phase 6: Seller Xu Ly Request

- Seller dashboard hien request duoc gan.
- Xem payload JSON snapshot va chi tiet build.
- Cap nhat status dung rule va timestamp.

### Phase 7: Audit, Validation Va Hardening

- Chuan hoa validation o service layer.
- Dam bao user inactive/bi ban khong thao tac duoc.
- Kiem tra seller chi xem/cap nhat request cua minh.
- Kiem tra admin action quan trong co audit log.
- Chuan hoa thong bao loi va refresh UI sau thao tac.

### Phase 8: MQTT/Realtime Notification (Da Trien Khai)

- Lop `Realtime/` (`MqttRealtimeService`, MQTTnet v4): buyer publish "request moi" -> seller reload; seller publish "status update" -> buyer reload.
- Database van la source of truth: luu DB truoc, publish sau, best-effort. Khong co broker thi app van chay DB-only (loi publish chi ghi log).
- Topic: `keyboard/seller/{sellerUserId}/build-request/new`, `keyboard/build-request/{requestId}/status/update`.
- Khong thay the Phase 8A chat SignalR.

### Phase 8A: Phase Phu Chat Realtime SignalR

- Phase phu, khong bat buoc cho MVP loi.
- Ho tro Buyer-Seller va Seller-Admin; khong co Buyer-Admin.
- Tin nhan luu DB truoc, SignalR chi dung de day realtime event.
- Khong lam unread count, online/offline indicator hoac typing indicator.
- Neu implement/demo phase nay, can test chat Buyer-Seller, Seller-Admin, chan Buyer-Admin va realtime khi ca hai ben online.

### Phase 9: Kiem Thu, Demo Va Bao Cao

- Test tao build hop le.
- Test build sai compatibility.
- Test buyer gui request.
- Test seller khong xem duoc request cua seller khac.
- Test admin ban user.
- Test soft delete linh kien.
- Kiem thu lai cac luong chinh cua MVP.
- Kiem thu chat SignalR chi khi Phase 8A duoc chon lam.
- Cap nhat bao cao/demo script.

### Phase 10: Dong Goi Va Ban Giao

- Ghi ro cach chay database va SignalR host neu co chat.
- Chot script database can chay.
- Ban giao lenh build/run va tai khoan demo.

## Lenh Chay Project

Chay ung dung WPF:

```powershell
dotnet run
```

Realtime MQTT (tuy chon): de bat realtime, chay mot broker MQTT o `localhost:1883` (vi du Mosquitto). Khong co broker thi app van chay binh thuong (DB-only); doi host/port qua `MqttSettings` trong `MainWindow.xaml.cs`.

Build project:

```powershell
dotnet build
```

Chay Phase 6 verification:

```powershell
dotnet run --project Phase6Verification\Phase6Verification.csproj
```

Verification (14 checks, ky vong `Passed: 14`) bao gom:

- Unit-style checks cho `BuildService`, `RequestService` (state machine, seller scoping T08/T09, chan seller unverified, realtime best-effort), `ChatService` (participant + admin-seller), `AccountService` (validate email/phone + login dung/sai/banned T01/T02) va `AdminService` (audit log).
- SQL integration: tao build tam/gui request/tao chat roi cleanup; **seed accounts login `Password123`** (admin/buyer/seller) + banned seed account bi chan.
- DB invariant checks tu `VerifyRefactor.sql`: total snapshot, switch quantity, exactly-one-FK, seller verified, conversation XOR, FK orphan, requested build/request, chat sender participant, password hash format.
- UI smoke manual/automation da kiem buyer/seller/admin login khong con popup `Loi (UI thread)`.

Ma tran test T01-T16: `Documents_Refactor/Phase9_Test_Matrix.md`. Kich ban demo 3 role: `Documents_Refactor/Phase9_Demo_Script.md`.

## Ghi Chu Thiet Ke

- `users` la bang tai khoan chung cho buyer, seller, admin.
- `roles` dung de phan quyen.
- `seller_profiles` chi luu thong tin mo rong cua seller.
- `is_active` dung cho ban/unban user.
- `is_available` dung cho soft delete linh kien.
- `build_requests` la bang trung tam cua seller workflow.
- `request_payload_json` giu snapshot cua build luc buyer gui yeu cau.
- `audit_log` giup truy vet cac thao tac quan trong.
