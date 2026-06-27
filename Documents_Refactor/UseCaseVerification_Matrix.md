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
| UC-01.0 | Khach dang ky tai khoan Buyer | Service/SQL | Covered | `AccountService.RegisterBuyerAsync` validates email/phone and creates role Buyer. UI flow can them AutomationId register da co. |
| UC-01.1 | Buyer quan ly tai khoan | Service/SQL + WPF UI smoke | Partial | Login `buyer_refactor` va profile qua user menu covered; logout UI flow planned. |
| UC-01.2 | Buyer mo Trang chu | WPF UI smoke | Covered | UI runner login buyer va xac nhan `BuyerDashboardRoot`. |
| UC-01.3 | Buyer mo Build cua toi | Service/SQL | Covered | `BuildService hides archived builds from the buyer list`; can them UI click nav build list/archive flow. |
| UC-01.4 | Buyer tao build moi / configurator | Service/SQL + VM | Covered | BuildService + Buyer VM compatible switch test covers 2.3-2.8. UI flow can them AutomationId configurator. |
| UC-01.4a | Buyer chon seller va gui request | Service/SQL | Covered | RequestService rejects unverified sellers + SQL integration CRUD. UI flow planned. |
| UC-01.5 | Buyer mo Request da gui | Service/SQL + WPF UI flow | Covered | Buyer request list + realtime reload tested at service level; UI QC summary covered by device QC scenario. |
| UC-01.6 | Buyer chat voi seller | Service/SQL | Covered | ChatService participant checks + SQL integration. UI flow planned. |
| UC-01.7 / UC-03.5 | Buyer nop don Seller, Admin duyet/tu choi | Service/SQL | Covered | SellerApplicationService guards + DB invariant + unique pending index. UI flow planned. |
| UC-02.1 | Seller quan ly tai khoan | WPF UI smoke | Partial | Login `seller_soigear` va profile qua user menu covered; logout UI flow planned. |
| UC-02.2 | Seller xu ly request duoc gan | Service/SQL + WPF UI flow | Partial | Request scoping, state machine 3.4-3.7 va snapshot render covered; full manual-click happy path van nam backlog. |
| UC-02.3 | Seller kiem tra QC keyboard | Service/SQL + WPF UI flow | Partial | UI runner co flow seller chay QC va buyer xem summary; can them case fail/warning neu can day du. |
| UC-02.4 | Seller phan tich | WPF UI flow | Covered | UI runner mo `NavAnalytics`, kiem tra chart/label va doi ngon ngu. |
| UC-02.5 | Seller chat | Service/SQL | Covered | ChatService supports buyer-seller va admin-seller conversations. UI flow planned. |
| UC-03.1 | Admin quan ly tai khoan | WPF UI smoke | Partial | Login `admin_refactor` va profile qua user menu covered; logout UI flow planned. |
| UC-03.2 | Admin mo Tong quan | Service/SQL + WPF UI smoke | Covered | StatsService auth + SQL analytics aggregates; UI runner xac nhan `AdminDashboardRoot`. |
| UC-03.3 | Admin mo Nguoi dung | Service/SQL + WPF UI flow | Covered | AdminService audit actions; UI runner mo tab `NavUser`. Full CRUD click planned. |
| UC-03.4 | Admin mo Seller | Service/SQL + WPF UI flow | Covered | Seller public/request eligibility + admin audit; UI runner mo tab `NavSeller`. Full edit click planned. |
| UC-03.5 | Admin mo Don xin Seller | Service/SQL + WPF UI flow | Covered | SellerApplicationService approve/reject covered; UI runner mo tab `NavApplications`. |
| UC-03.6 | Admin mo Brand | WPF UI flow | Partial | UI runner mo tab `NavBrand`; can add explicit brand CRUD scenario. |
| UC-03.7 | Admin mo Linh kien | Service/SQL + WPF UI flow | Partial | Component repository/service used by app; UI runner mo tab `NavComponent`; can add explicit CRUD scenario. |
| UC-03.8 | Admin mo Audit log | Service/SQL + WPF UI flow | Covered | Admin audit tests + SQL seed invariant; UI runner mo tab `NavAudit`. |
| UC-03.9 | Admin chat voi seller | Service/SQL | Covered | ChatService admin-seller test. UI flow planned. |
| AD-01 | Tai khoan | Service/SQL + WPF UI smoke | Partial | Login + xem profile qua user menu covered; full register/logout UI flow planned. |
| AD-02 | Buyer tao va gui build | Service/SQL + VM | Partial | Kit-based flow covered at service/VM level; full UI click flow planned. |
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
