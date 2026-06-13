# Phase 9 — Demo Script (3 Role) + Screenshot Checklist

**Cập nhật:** 13/06/2026
**Mục tiêu:** kịch bản bảo vệ/demo chạy hết các luồng chính (Admin / Buyer / Seller) trên DB đã seed.

---

## 0. Chuẩn bị

1. **SQL Server** chạy, DB `CustomKeyboard_Refactor` đã chạy `CreateSchema_Refactor.sql` + `SeedData_Refactor.sql`.
2. **(Tùy chọn) MQTT broker** ở `localhost:1883` (vd Mosquitto) nếu muốn demo realtime request/status — không có broker thì app vẫn chạy DB-only.
3. Chạy app:
   ```powershell
   dotnet run --project Custom_keyboard.csproj
   ```
4. (Khuyến nghị) chạy `dotnet run --project Phase6Verification/Phase6Verification.csproj` trước, chụp màn hình **14/14 PASS** làm bằng chứng test.

### Tài khoản seed (mật khẩu `Password123`)

| Username | Role | Ghi chú |
|---|---|---|
| `admin_refactor` | Admin | quản trị |
| `buyer_refactor` | Buyer | buyer chính (đã có sẵn build/request mẫu) |
| `buyer_second` | Buyer | buyer phụ |
| `buyer_inactive` | Buyer | **bị khóa** (`is_active=0`) — demo T02 |
| `seller_soigear` | Seller | **verified** |
| `seller_keyboardlab` | Seller | **verified** |
| `seller_unverified` | Seller | **chưa verified** — demo T07 |

> ✅ **Mật khẩu seed (Phase 10 — đã gỡ blocker):** `SeedData_Refactor.sql` nay nhúng **hash PBKDF2 thật của `Password123`** cho mọi tài khoản → clean machine login được ngay sau khi seed. Có test tự động `SQL integration: seed accounts log in with Password123` chứng minh. Xem `Phase10_Handover.md`.

---

## 1. Kịch bản A — Admin (`admin_refactor`)

| Bước | Thao tác | Kỳ vọng | Test |
|---|---|---|---|
| A1 | Login `admin_refactor` / `Password123` | Vào Admin Dashboard | T01 |
| A2 | Tab Sellers → Verify `seller_unverified`, **rồi Unverify lại ngay** | Lần Verify: chuyển verified + audit; lần Unverify: về unverified + audit lần 2 | T03/T12 |
| A3 | Tab Catalog (Kit/Switch/Keycap/Stabilizer/Accessory) → sửa giá 1 item, ẩn/khôi phục 1 item | Cập nhật + ghi audit | T03 |
| A4 | Tab Users → ban một buyer (rồi unban) | `is_active` đổi; ghi audit | T02/T12 |
| A5 | Xem Audit Log | Thấy các action vừa làm, attribute đúng admin | T12 |

> ⚠️ **Thứ tự quan trọng:** A2 phải kết thúc ở trạng thái `seller_unverified` **chưa verified**, vì kịch bản B6 (T07) cần đúng seller này còn unverified để demo bị chặn. Verify→Unverify ngay trong A2 vừa demo được cả hai chiều + 2 dòng audit, vừa giữ state cho B6. (Hai seller verified sẵn để dùng chung: `seller_soigear`, `seller_keyboardlab`.)

## 2. Kịch bản B — Buyer (`buyer_refactor`)

| Bước | Thao tác | Kỳ vọng | Test |
|---|---|---|---|
| B0 | (Tùy chọn) đăng ký buyer mới: email/phone sai format | Bị chặn với message rõ | T01 |
| B0' | Thử login `buyer_inactive` / `Password123` | Bị chặn "Tài khoản đang bị khóa" | **T02** |
| B1 | Login `buyer_refactor` | Vào Buyer Dashboard | T01 |
| B2 | Tạo build: chọn Kit → thêm Switch đủ số lượng + Keycap + Stabilizer + Accessory | Tổng giá realtime; validation Info/Warning/Error | T04 |
| B3 | Cố ý chọn Switch sai tech/mount so với Kit | Hiện **Error** compatibility, không cho gửi | **T05** |
| B4 | Sửa lại cho hợp lệ → Lưu build | Build `Saved`, tổng giá khớp snapshot | T04 |
| B5 | Gửi request tới `seller_soigear` (verified) | Request `Pending`, snapshot kit+items | **T06** |
| B6 | Thử gửi tới `seller_unverified` | Bị chặn "seller chưa verified" | **T07** |
| B7 | Mở Chat → nhắn `seller_soigear` | Tin lưu DB, hiện trong lịch sử | T13 |

## 3. Kịch bản C — Seller (`seller_soigear`)

| Bước | Thao tác | Kỳ vọng | Test |
|---|---|---|---|
| C1 | Login `seller_soigear` | Vào Seller Dashboard, thấy request từ B5 | T08 |
| C2 | Pending → Accepted | Hợp lệ; ghi `accepted_at` | **T10** |
| C3 | **Đang ở Accepted**, thử nhảy thẳng Accepted → Completed | Bị chặn rõ ràng (chưa qua In_progress) | **T11** |
| C4 | Tiếp tục Accepted → In_progress → Completed | Mỗi bước hợp lệ; ghi `completed_at` | **T10** |
| C5 | Mở Chat → trả lời buyer; mở hội thoại với admin | Tin lưu DB, 2 chiều | T13/**T14** |

> T09 (seller không thấy request của seller khác) thể hiện gián tiếp: queue của `seller_soigear` chỉ chứa request gán cho mình — không có request của `seller_keyboardlab`.

## 4. (Tùy chọn) Realtime MQTT — Phase 8

Mở 2 instance (vd Buyer máy này, Seller máy khác / cửa sổ khác), bật broker `localhost:1883`:

1. Buyer gửi request mới → Seller Dashboard **tự reload** thấy request (không cần bấm refresh).
2. Seller đổi status → Buyer Dashboard tự reload.
3. Tắt broker giữa chừng → vẫn gửi/đổi được (DB-first), bật lại broker reconnect.

> T16 (chat realtime SignalR) **defer** — xem `Phase8A_SignalR_Hotspot_Demo_Plan.md`.

---

## 5. Screenshot checklist

Chụp tối thiểu các ảnh sau (đặt trong `Documents_Refactor/screenshots/` đề xuất):

- [ ] `00-tests-14pass.png` — runner 14/14 PASS.
- [ ] `01-login.png` — màn login.
- [ ] `02-admin-audit.png` — Audit Log sau khi verify/ban.
- [ ] `03-admin-catalog.png` — quản lý catalog.
- [ ] `04-buyer-build-valid.png` — build hợp lệ + tổng giá.
- [ ] `05-buyer-build-error.png` — cảnh báo compatibility (T05).
- [ ] `06-buyer-send-request.png` — gửi request thành công.
- [ ] `07-buyer-blocked-unverified.png` — chặn seller unverified (T07).
- [ ] `08-seller-requests.png` — seller xem request.
- [ ] `09-seller-status.png` — chuyển status.
- [ ] `10-chat.png` — chat buyer↔seller / admin↔seller.
- [ ] `11-banned-login.png` — `buyer_inactive` bị chặn (T02).

> Blocker mật khẩu đã gỡ (mục 0) nên login chụp được ngay sau khi seed. Ảnh `00-tests-14pass.png` (runner) là bằng chứng test tự động; phần ảnh UI chụp khi chạy GUI demo.
