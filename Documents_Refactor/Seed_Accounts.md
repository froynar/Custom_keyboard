# Seed Accounts

Danh sach tai khoan demo duoc seed tu `Documents_Refactor/SeedData_Refactor.sql`.

Mat khau chung cho cac tai khoan seed: `Password123`

Co the dang nhap bang `username` hoac `email`.

| Username | Email | Role | Phone | Trang thai | Ghi chu |
| --- | --- | --- | --- | --- | --- |
| `admin_refactor` | `admin.refactor@example.com` | Admin | `0900000101` | Active | Quan tri user/seller/catalog, audit, duyet don seller. |
| `buyer_refactor` | `buyer.refactor@example.com` | Buyer | `0900000102` | Active | Buyer chinh de demo tao build, gui request va chat. |
| `buyer_second` | `buyer.second@example.com` | Buyer | `0900000103` | Active | Buyer phu de test du lieu nhieu nguoi mua. |
| `buyer_inactive` | `buyer.inactive@example.com` | Buyer | `0900000104` | Inactive/Banned | Dung de test tai khoan bi khoa; dung mat khau van bi chan login. |
| `seller_soigear` | `seller.soigear@example.com` | Seller | `0900000201` | Active, verified | Seller chinh; nhan request, cap nhat status va chat. |
| `seller_keyboardlab` | `seller.keyboardlab@example.com` | Seller | `0900000202` | Active, verified | Seller verified thu hai. |
| `seller_unverified` | `seller.unverified@example.com` | Seller | `0900000203` | Active, unverified | Login duoc, nhung Buyer khong gui request duoc vi seller chua verified. |

## Ghi Chu Nhanh

- `buyer_inactive` co `is_active = 0`, nen service se chan dang nhap.
- `seller_unverified` co role Seller va active, nhung seller profile chua verified.
- Buyer chi chon duoc seller active va verified de gui request.
- Seed script la idempotent: chay lai `SeedData_Refactor.sql` se cap nhat lai email, phone, role, status va password hash ve dung du lieu seed.
