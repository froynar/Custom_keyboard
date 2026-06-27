# Use Case Verification Matrix

Muc tieu: bien use cases va activity diagrams thanh cac kich ban co the chay lap lai. Ma tran nay tach 3 lop kiem thu:

- `Service/SQL`: runner khong mo UI, dung service/repository + DB invariant. On dinh nhat cho nghiep vu.
- `WPF UI smoke`: mo app that, login/click theo `AutomationProperties.AutomationId`, xac nhan man hinh dung role.
- `WPF UI flow`: mo app that va click het flow nguoi dung. Can them AutomationId cho control cua tung flow.

## Runner

| Runner | Lenh | Muc dich |
| --- | --- | --- |
| `Phase6Verification` | `dotnet run --project Phase6Verification\Phase6Verification.csproj` | Kiem tra service, SQL integration, analytics, seller application, auth seed va invariant DB. |
| `WpfUiVerification` | `dotnet run --project WpfUiVerification\WpfUiVerification.csproj` | Mo app WPF that, smoke login buyer/seller/admin va mo Profile settings bang UI Automation. Can build app truoc. |

Neu app exe khong nam o `bin\Debug\net10.0-windows\Custom_keyboard.exe`, truyen duong dan:

```powershell
dotnet run --project WpfUiVerification\WpfUiVerification.csproj -- --app=D:\path\Custom_keyboard.exe
```

## Coverage Matrix

| ID | Use case / activity | Layer hien co | Status | Ghi chu / viec tiep theo |
| --- | --- | --- | --- | --- |
| UC-01.1 | Khach dang ky tai khoan Buyer | Service/SQL | Covered | `AccountService.RegisterBuyerAsync` validates email/phone and creates role Buyer. UI flow can them AutomationId register da co. |
| UC-01.2 | Buyer dang nhap | Service/SQL + WPF UI smoke | Covered | UI runner login `buyer_refactor` va xac nhan `BuyerDashboardRoot`. |
| UC-01.3 | Buyer dang xuat | WPF UI flow | Planned | Can AutomationId cho nut logout tren dashboard. |
| UC-01.4 | Xem profile tai khoan | WPF UI smoke | Covered | UI runner mo user menu va bam `UserMenuProfileButton`, xac nhan `UserMenuProfileDetails` hien tren buyer/seller/admin. |
| UC-01.2.1 | Xem dashboard buyer | WPF UI smoke | Covered | `BuyerDashboardRoot`. |
| UC-01.2.2 | Xem danh sach build | Service/SQL | Covered | `BuildService hides archived builds from the buyer list`; can them UI click nav build list. |
| UC-01.2.3-2.8 | Tao build, chon kit/linh kien, validation, tong gia, luu build | Service/SQL + VM | Covered | BuildService + Buyer VM compatible switch test. UI flow can them AutomationId configurator. |
| UC-01.2.9-2.10 | Chon seller verified va gui request | Service/SQL | Covered | RequestService rejects unverified sellers + SQL integration CRUD. UI flow planned. |
| UC-01.2.11 | Theo doi request | Service/SQL | Covered | Buyer request list + realtime reload tested at service level. |
| UC-01.2.12 | Luu tru build | Service/SQL | Covered | Archived builds hidden from buyer list. |
| UC-01.2.13 / UC-03.4.6 | Buyer nop don seller, admin duyet/tu choi | Service/SQL | Covered | SellerApplicationService guards + DB invariant + unique pending index. UI flow planned. |
| UC-01.5.1 | Buyer chat voi seller | Service/SQL | Covered | ChatService participant checks + SQL integration. UI flow planned. |
| UC-02.1 | Seller dang nhap | WPF UI smoke | Covered | UI runner login `seller_soigear` va xac nhan `SellerDashboardRoot`. |
| UC-02.3.1-3.3 | Seller xem dashboard/request/detail | Service/SQL + WPF UI smoke | Partial | Request scoping covered; UI detail flow planned. |
| UC-02.3.4-3.7 | Seller cap nhat request Accepted/In_progress/Completed/Cancelled | Service/SQL | Covered | RequestService state machine + seller ownership tests. |
| UC-02.5.2 | Seller chat voi buyer | Service/SQL | Covered | ChatService supports buyer-seller conversation. |
| UC-02.5.3 | Seller chat voi admin | Service/SQL | Covered | ChatService supports admin-seller conversation. |
| UC-03.1 | Admin dang nhap/dashboard | WPF UI smoke | Covered | UI runner login `admin_refactor` va xac nhan `AdminDashboardRoot`. |
| UC-03.4.1 | Admin dashboard/analytics | Service/SQL + WPF UI smoke | Covered | StatsService auth + SQL analytics aggregates. |
| UC-03.4.2 | Quan ly user | Service/SQL | Covered | AdminService audit actions; UI flow planned. |
| UC-03.4.3 | Quan ly seller profile/verify | Service/SQL | Covered | Seller public/request eligibility + admin audit. UI flow planned. |
| UC-03.4.4 | Quan ly catalog | Service/SQL | Partial | Component repository/service used by app; can add explicit CRUD scenario. |
| UC-03.4.5 | Xem audit log | Service/SQL | Covered | Admin audit tests + SQL seed invariant. UI flow planned. |
| UC-03.5.4 | Admin chat voi seller | Service/SQL | Covered | ChatService admin-seller test. |
| AD-01 | Tai khoan | Service/SQL + WPF UI smoke | Partial | Login + xem profile qua user menu covered; full register/logout UI flow planned. |
| AD-02 | Buyer tao va gui build | Service/SQL + VM | Partial | Activity doc cu con nhac case/PCB/plate; can cap nhat sang kit-based truoc khi UI flow full. |
| AD-03 | Seller xu ly request | Service/SQL + WPF UI smoke | Partial | Status state machine covered; full UI click planned. |
| AD-04 | Admin quan tri | Service/SQL + WPF UI smoke | Partial | Admin login covered; full tab CRUD click planned. |
| AD-05 | Chat realtime | Service/SQL | Covered | DB-first chat covered; realtime best-effort covered by request notifier test. |

## UI Automation Backlog

1. Them AutomationId cho nut logout, sidebar nav, configurator input, request grid, seller status buttons, admin tabs va application buttons.
2. Them `WpfUiVerification` scenario register buyer -> logout -> login buyer.
3. Them buyer UI flow tao build -> luu -> gui request.
4. Them seller UI flow nhan request -> Accepted -> In_progress -> Completed.
5. Them buyer application UI flow -> admin approve/reject UI flow.
6. Them screenshot capture khi fail de debug visual/layout nhanh hon.
