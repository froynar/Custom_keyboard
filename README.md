# Custom Keyboard Builder

Phan mem ho tro buyer tao cau hinh ban phim co ban, gui yeu cau build cho seller, va cho admin quan ly nguoi dung cung linh kien.

Du an hien tai la ung dung desktop WPF dung .NET.

## Muc Tieu

He thong tap trung vao phan mem quan ly build keyboard, khong di qua sau vao mo phong ky thuat ban phim. MVP can lam duoc:

- Buyer dang ky, dang nhap, tao cau hinh keyboard custom.
- Buyer chon layout, case, PCB, plate, switch, keycap, stabilizer va mod co ban.
- Buyer gui yeu cau build cho seller.
- Backend luu yeu cau vao database va forward realtime qua MQTT.
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

## Kien Truc Du Kien

```text
Buyer App
  -> MQTT publish build request
  -> MQTT Broker
  -> Backend Server
      -> Save request to Database
      -> Publish request to Seller Dashboard topic
  -> Seller Dashboard receives realtime request
```

Database la source of truth. MQTT chi dung cho realtime delivery.

Neu seller offline, backend van phai luu request trong database. Khi seller mo dashboard, app lay lai danh sach request tu backend/database.

## Cong Nghe Du Kien

- Desktop app: WPF, .NET
- Database: SQL Server, PostgreSQL, MySQL, hoac SQLite cho MVP
- Realtime messaging: MQTT
- Backend API: ASP.NET Core Web API
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
- `switches`: switch.
- `keycap_sets`: bo keycap.
- `cases`: case keyboard.
- `pcbs`: PCB.
- `plates`: plate.
- `stabilizers`: stabilizer.

Moi bang linh kien co `is_available` de ho tro soft delete.

### Compatibility

- `case_layouts`: case ho tro layout nao.
- `pcb_layouts`: PCB ho tro layout nao.
- `plate_layouts`: plate ho tro layout nao.
- `compatibility_rules`: rule kiem tra do tuong thich giua case, PCB, plate; app kiem tra them PCB technology voi switch technology va `pcb.switch_mount` khop voi `switch.mount_type`.

### Build Flow

- `builds`: cau hinh build cua buyer.
- `build_mods`: mod co ban nhu lube, film, spring.
- `build_requests`: yeu cau build buyer gui cho seller.

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

### Phase 8: MQTT/Realtime Notification Neu Con Thoi Gian

- Phase phu, co the de sau MVP.
- Dung cho notification request/status neu can demo realtime rieng.
- Database van la source of truth.
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

Build project:

```powershell
dotnet build
```

## Ghi Chu Thiet Ke

- `users` la bang tai khoan chung cho buyer, seller, admin.
- `roles` dung de phan quyen.
- `seller_profiles` chi luu thong tin mo rong cua seller.
- `is_active` dung cho ban/unban user.
- `is_available` dung cho soft delete linh kien.
- `build_requests` la bang trung tam cua seller workflow.
- `request_payload_json` giu snapshot cua build luc buyer gui yeu cau.
- `audit_log` giup truy vet cac thao tac quan trong.
