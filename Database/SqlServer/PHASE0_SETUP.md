# Phase 0 — Setup cần chạy trên máy Windows

Sandbox refactor (Linux) **không** chạy được `dotnet build` (WPF) và `sqlcmd`. Hai việc dưới đây bạn chạy trên máy Windows để hoàn tất Phase 0.

## 1. Xác nhận build hiện tại pass (mốc so sánh)

```powershell
cd D:\ccdmm\cc3\Custom_keyboard
dotnet build
```

Mục tiêu: build pass trước khi refactor, để mọi lỗi compile sau này là do refactor sinh ra (không phải lỗi có sẵn).

## 2. Tạo database test riêng cho refactor

Tạo DB rỗng riêng, **không** đụng DB runtime cũ. Ví dụ với `sqlcmd`:

```powershell
sqlcmd -S localhost -Q "IF DB_ID('CustomKeyboard_Refactor') IS NULL CREATE DATABASE CustomKeyboard_Refactor;"
```

(hoặc tạo bằng SSMS). DB này sẽ được dùng ở Phase 1 để chạy `CreateSchema_Refactor.sql` → `SeedData_Refactor.sql` → `VerifyRefactor.sql`.

> Lưu ý: chuỗi kết nối trong app (`Data/SqlServer/SqlServerSettings.cs` / cấu hình) chỉ trỏ sang DB refactor khi đã sẵn sàng — giữ DB cũ nguyên vẹn trong lúc refactor.

## 3. (Tùy chọn) Dọn lock git nếu thấy `.git/index.lock`

Trong lúc thao tác từ sandbox, mount Windows có thể để lại file lock. Nếu git báo "index.lock exists":

```powershell
cd D:\ccdmm\cc3\Custom_keyboard
del .git\index.lock
del .git\index.lock.stale  # nếu có
```

## Trạng thái Phase 0 (đã làm trong sandbox)

- [x] Tạo nhánh `refactor/kit-based-erd` + commit checkpoint `pre-refactor`.
- [x] Backup toàn bộ SQL cũ vào `Database/SqlServer/_legacy/`.
- [ ] Xác nhận `dotnet build` pass (chạy trên Windows — mục 1).
- [ ] Tạo DB test `CustomKeyboard_Refactor` (chạy trên Windows — mục 2).
