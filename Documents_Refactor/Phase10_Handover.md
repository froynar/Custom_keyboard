# Phase 10 — Đóng Gói & Bàn Giao (Clean-Machine Setup)

**Cập nhật:** 13/06/2026
**Mục tiêu:** người khác (máy mới / lúc chấm điểm) chạy được end-to-end theo hướng dẫn này.

---

## 1. Yêu cầu môi trường

- **SQL Server** (Express trở lên) — mặc định instance `KHOADZS1VN\SQLEXPRESS`, Windows Integrated Security.
- **.NET 10 SDK** (project target `net10.0-windows`, WPF → chỉ chạy trên Windows).
- (Tùy chọn) **SSMS** hoặc `sqlcmd` để chạy script DB.
- (Tùy chọn) **MQTT broker** ở `localhost:1883` (vd Mosquitto) nếu muốn demo realtime Phase 8.

`sqlcmd` trên máy dev không nằm trong PATH; đường dẫn đầy đủ:
```
C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE
```

---

## 2. Các bước setup (clean machine)

### B1. Trỏ đúng SQL Server

Nếu instance **khác** `KHOADZS1VN\SQLEXPRESS`, sửa `Server` trong `Data/SqlServer/SqlServerSettings.cs`:

```csharp
public string Server { get; set; } = @"KHOADZS1VN\SQLEXPRESS"; // đổi sang instance của bạn
public string Database { get; set; } = "CustomKeyboard_Refactor";
public bool IntegratedSecurity { get; set; } = true; // Windows auth
```

> Hiện app cấu hình SQL bằng hằng số trong source (chưa có file config ngoài) — đổi ở đây rồi `dotnet build` lại.

### B2. Tạo schema (tự tạo database)

`CreateSchema_Refactor.sql` **tự tạo** DB `CustomKeyboard_Refactor` nếu chưa có (`IF DB_ID(...) IS NULL CREATE DATABASE` + `USE`), rồi drop+create 17 bảng (idempotent). Vì DB có thể chưa tồn tại, chạy script này **không kèm `-d`**:

```powershell
$sqlcmd = "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE"
& $sqlcmd -S "KHOADZS1VN\SQLEXPRESS" -E -C -b -i "Database\SqlServer\CreateSchema_Refactor.sql"
```

### B3. Seed dữ liệu (gồm tài khoản demo)

```powershell
& $sqlcmd -S "KHOADZS1VN\SQLEXPRESS" -E -C -b -d "CustomKeyboard_Refactor" -i "Documents_Refactor\SeedData_Refactor.sql"
```

> Seed này (MERGE, idempotent) đã nhúng **hash PBKDF2 thật của `Password123`** cho mọi tài khoản — không còn bước reset mật khẩu riêng. Chạy lại seed cũng sửa được DB còn giữ hash placeholder cũ.

### B4. (Tùy chọn) Kiểm tra invariant DB

```powershell
& $sqlcmd -S "KHOADZS1VN\SQLEXPRESS" -E -C -b -d "CustomKeyboard_Refactor" -i "Database\SqlServer\VerifyRefactor.sql"
```

### B5. Build & chạy app

```powershell
dotnet build
dotnet run
```

### B6. Chạy verification runner (khuyến nghị trước khi demo)

```powershell
dotnet run --project Phase6Verification\Phase6Verification.csproj
```

Kỳ vọng: **`Passed: 14` / `Failed: 0`** (8 unit + 1 realtime best-effort + 3 SQL integration + 1 invariant; gồm `SQL integration: seed accounts log in with Password123`). Cần SQL Server + DB đã seed.

---

## 3. Tài khoản seed (mật khẩu: `Password123`)

| Username | Role | Trạng thái | Dùng để demo |
|---|---|---|---|
| `admin_refactor` | Admin | active | quản trị / audit |
| `buyer_refactor` | Buyer | active | build + request + chat (có sẵn build/request mẫu) |
| `buyer_second` | Buyer | active | buyer phụ |
| `buyer_inactive` | Buyer | **banned** (`is_active=0`) | **T02** — bị khóa, login bị chặn |
| `seller_soigear` | Seller | **verified** | nhận request, đổi status, chat |
| `seller_keyboardlab` | Seller | **verified** | seller verified thứ 2 |
| `seller_unverified` | Seller | **chưa verified** | **T07** — buyer không gửi request được |

> Login bằng username **hoặc** email + `Password123`. `buyer_inactive` dù đúng mật khẩu vẫn bị chặn (`InactiveUser`) — đã có test tự động chứng minh (`SQL integration: seed accounts log in with Password123`).

---

## 4. Hành vi DB rỗng vs đã seed

- **Chỉ schema (chưa seed):** app build & chạy. Chưa có user → login luôn báo sai thông tin; có thể **đăng ký** buyer mới rồi dùng. Catalog rỗng → build configurator không có kit để chọn (đúng kỳ vọng, không crash — đã có global handler Phase 7).
- **Đã seed:** đầy đủ tài khoản + catalog + build/request/chat mẫu → demo được toàn bộ luồng.

> Kiểm tra "DB rỗng không crash" là bước **manual** (chạy GUI). Verification runner kiểm tầng non-UI + DB đã seed.

---

## 5. Gói bàn giao (checklist)

- [ ] Source (repo nhánh `refactor/kit-based-erd`).
- [ ] Scripts DB: `Database/SqlServer/CreateSchema_Refactor.sql`, `Documents_Refactor/SeedData_Refactor.sql`, `Database/SqlServer/VerifyRefactor.sql`.
- [ ] Tài liệu: roadmap, `Phase9_Test_Matrix.md`, `Phase9_Demo_Script.md`, file này.
- [ ] Ảnh chụp: runner `14/14 PASS` + screenshot 3 role (xem checklist trong `Phase9_Demo_Script.md`).
- [ ] Báo cáo (nếu yêu cầu nộp).

---

## 6. Sự cố thường gặp

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| `Login` luôn sai dù đúng mật khẩu | DB còn hash placeholder cũ | Chạy lại B3 (seed cập nhật hash `Password123`) |
| `sqlcmd ... Cannot open database "CustomKeyboard_Refactor"` ở B2 | Chạy CreateSchema kèm `-d` khi DB chưa tồn tại | Bỏ `-d` ở B2 (script tự `CREATE DATABASE` + `USE`) |
| Runner fail phần SQL integration | Sai instance / chưa seed | Kiểm `SqlServerSettings.Server` + chạy B2–B3 |
| App login được nhưng realtime không chạy | Chưa bật broker MQTT | Không bắt buộc; bật Mosquitto `localhost:1883` nếu muốn (đổi qua `MqttSettings` trong `MainWindow.xaml.cs`) |
| Lỗi nền/DB | — | Xem log `%LOCALAPPDATA%/CustomKeyboard/log.txt` (Phase 7) |
