# Phase 9 — Test Matrix (T01–T16)

**Cập nhật:** 13/06/2026
**Nguồn sự thật:** code nhánh `refactor/kit-based-erd` + runner `Phase6Verification`.
**Kết quả tự động:** `Phase6Verification` **18/18 PASS** (unit checks + realtime best-effort + SQL build/request/chat + SQL analytics aggregates, **seed-account login `Password123`** (Phase 10), stats service role/active guard, buyer dashboard switch filtering, invariant queries, requested-build/archive visibility review fixes).

> Ghi chú: bộ test vẫn mang tên lịch sử "Phase 6 verification" nhưng nay phủ cả các case Phase 9. Đổi tên project là việc cosmetic, chưa làm để tránh vỡ tham chiếu trong docs/roadmap.

---

## 1. Cách chạy

```powershell
# Yêu cầu: SQL Server (KHOADZS1VN\SQLEXPRESS) + DB CustomKeyboard_Refactor đã seed.
dotnet run --project Phase6Verification/Phase6Verification.csproj
```

- **Unit test** (T01, T02, T04, T05, T07, T08, T09, T10, T11, T12, T14) chạy độc lập, **không cần DB**.
- **SQL integration** (T06, T13 + invariant nền) cần DB đã chạy `CreateSchema_Refactor.sql` + `SeedData_Refactor.sql`.
- Kết quả: in `PASS/FAIL` từng case + tổng `Passed/Failed`; exit code `0` nếu sạch.

---

## 2. Ma trận T01–T16

| Mã | Test case | Trạng thái | Phủ bởi (case trong runner) |
|---|---|---|---|
| **T01** | Khách đăng ký tài khoản Buyer và Buyer đăng nhập | ✅ Pass | `AccountService validates email/phone on register` (đăng ký tạo role Buyer) + `AccountService login accepts valid and blocks wrong/banned` (đăng nhập đúng → set session) |
| **T02** | User bị ban không đăng nhập được | ✅ Pass | `...login accepts valid and blocks wrong/banned` — user `is_active = 0` + mật khẩu đúng → `InactiveUser`; test `Logout()` trước rồi thử banned-login và assert `CurrentUser` vẫn `null` (không tạo session mới) |
| **T03** | Admin thêm/sửa/ẩn/khôi phục linh kiện | 🟡 Một phần | Tự động: `AdminService writes audit entries...` (tạo brand + ghi audit) + invariant catalog (giá ≥ 0, kit switch qty). **Ẩn/khôi phục** (`SetComponentAvailabilityAsync`) hiện kiểm thủ công trên UI Admin |
| **T04** | Buyer tạo build hợp lệ và lưu | ✅ Pass | `BuildService validates totals and applies snapshots` (tổng giá + snapshot 5 item) |
| **T05** | Buyer tạo build sai compatibility → cảnh báo | ✅ Pass | `BuildService rejects incompatible switch technology` (switch tech/mount lệch kit → Error) |
| **T06** | Buyer gửi request cho seller verified | ✅ Pass | `SQL integration covers build/request/chat CRUD` (gửi tới seller verified thật) + nhánh sanity trong `...rejects requests to unverified sellers` |
| **T07** | Buyer không gửi được request cho seller unverified | ✅ Pass | `RequestService rejects requests to unverified sellers` |
| **T08** | Seller xem request được gán | ✅ Pass | `RequestService scopes requests to the owning seller` — seller chủ sở hữu thấy request trong queue |
| **T09** | Seller không xem/sửa request của seller khác | ✅ Pass | `RequestService scopes requests to the owning seller` — seller khác không thấy + `UpdateStatus` ném lỗi "khong thuoc seller" |
| **T10** | Seller cập nhật status đúng rule | ✅ Pass | `RequestService enforces request status state machine` (Pending→Accepted→In_progress→Completed + timestamp) |
| **T11** | Seller không cập nhật status sai rule | ✅ Pass | `...state machine` — Accepted→Completed bị chặn |
| **T12** | Admin xem audit log | ✅ Pass | `AdminService writes audit entries...` — ghi 3 action (ban/verify/catalog), attribute đúng admin; non-admin không ghi audit |
| **T13** | Buyer chat seller, lưu DB + hiện lịch sử | ✅ Pass | `ChatService enforces participants and verified sellers` + `SQL integration...chat CRUD` |
| **T14** | Seller chat admin, lưu DB + hiện lịch sử | ✅ Pass | `ChatService supports admin-seller conversations` — admin↔seller, lưu DB, seller đọc lại lịch sử |
| **T15** | Buyer không chat trực tiếp Admin | ✅ Pass (cấu trúc) | API chỉ cho buyer-seller & admin-seller (không có path buyer-admin) + invariant DB "Conversation XOR participant rule" |
| **T16** | Hai user online nhận tin qua SignalR event | ⏸️ Defer | Phụ thuộc Phase 8A (SignalR) — **chưa triển khai**; xem `Phase8A_SignalR_Hotspot_Demo_Plan.md` |

**Tổng:** **14 đạt + 1 một phần (T03) + 1 defer (T16)**.
- T03 có phần tự động (tạo brand + ghi audit + invariant catalog), nhưng **ẩn/khôi phục component** (`SetComponentAvailabilityAsync`) chưa có unit → kiểm thủ công trên UI.
- T16 defer theo Phase 8A (SignalR) tùy chọn.

---

## 3. Invariant nền (ngoài T01–T16, chạy kèm trong runner)

`VerifyRefactor invariant queries return clean results` kiểm 18 bảng + các bất biến nghiệp vụ, gồm:

- Row count seed đúng kỳ vọng (cho phép baseline ≥ ở các bảng giao dịch).
- User bắt buộc đủ field, không trùng username/email/phone.
- **Active user password hash đúng định dạng PBKDF2** (`PBKDF2-SHA256$100000$...`).
- Build total = giá kit + Σ(qty × unit price); switch qty khớp `required_switch_quantity`.
- `build_items` mỗi dòng đúng 1 FK sản phẩm; FK không mồ côi.
- Seller nhận request phải verified + active.
- Conversation XOR participant; chat sender phải là participant.
- Catalog giá ≥ 0; kit `required_switch_quantity > 0`.

Cùng `RequestService publishes realtime after DB write (best-effort)`: publish đúng id/status **sau** khi lưu DB, và notifier lỗi **không** phá DB write (hợp đồng Phase 8).

---

## 4. Khoảng trống còn lại

- **T03 (ẩn/khôi phục linh kiện):** chưa có unit cho `SetComponentAvailabilityAsync` ở tầng service — hiện dựa vào UI smoke. Có thể bổ sung 1 unit nếu cần phủ tự động hoàn toàn.
- **T16 (SignalR realtime):** defer theo Phase 8A.
- **UI polish + screenshot:** thuộc phần "Hoàn thiện demo" (xem `Phase9_Demo_Script.md`). Blocker mật khẩu seed đã gỡ ở Phase 10 (`SeedData_Refactor.sql` nhúng hash thật `Password123` + test `seed-account login`) → login/screenshot chụp được; còn lại là chạy GUI chụp ảnh (manual).
