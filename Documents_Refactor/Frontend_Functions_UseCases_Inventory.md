# Custom Keyboard Builder - Frontend Functions And Use Cases Inventory

Tai lieu nay tong hop cac man hinh, nut chuc nang, controls, luong use case va cac ghi chu lien quan den frontend hien tai de phuc vu viec cai tao UI/UX.

Pham vi khao sat:

- `MainWindow.xaml`
- `Views/*.xaml`
- `ViewModels/*ViewModel.cs`
- `Themes/*.xaml`
- `Localization/AppStrings.cs`
- `Documents_Refactor/Custom_Keyboard_Use_Cases_Refactor.md`
- `WpfUiVerification/Program.cs`

Tai lieu nay chi la inventory/frontend brief, khong kem sua code.

## 1. Tong Quan Frontend

Ung dung la WPF desktop app theo mo hinh MVVM. `MainWindow` la shell chinh, dung `ContentControl` de hien view theo `CurrentViewModel`.

Mapping view hien tai:

| ViewModel | View |
| --- | --- |
| `LoginViewModel` | `LoginView` |
| `RegisterViewModel` | `RegisterView` |
| `BuyerDashboardViewModel` | `BuyerDashboardView` |
| `SellerDashboardViewModel` | `SellerDashboardView` |
| `AdminDashboardViewModel` | `AdminDashboardView` |

Thanh phan dung chung:

| Thanh phan | Mo ta |
| --- | --- |
| `ChatView` | Panel chat duoc nhung trong Buyer, Seller, Admin dashboard. |
| `UserMenuView` | Menu nguoi dung goc phai, dung chung tren dashboard. |
| `Themes/Colors.xaml` | Mau sidebar, accent, status, text, background. |
| `Themes/Controls.xaml` | Style Button, SidebarButton, SidebarNav, DataGrid, Card, Badge, Empty state. |
| `Themes/Typography.xaml` | Font Segoe UI va font sizes co ban. |

Kich thuoc shell:

| Thuoc tinh | Gia tri |
| --- | --- |
| Window size | 1120 x 720 |
| Minimum size | 980 x 620 |
| Startup | Center screen |

## 2. Theme Va Design System Hien Tai

### 2.1 Mau Sac

| Token | Gia tri | Vai tro |
| --- | --- | --- |
| `SidebarBgColor` | `#2A3F54` | Nen sidebar |
| `SidebarBgAltColor` | `#233648` | Nen hover/sidebar footer |
| `SidebarTextColor` | `#ECF0F1` | Text sidebar |
| `AccentColor` | `#1ABB9C` | Mau nhan chinh |
| `AccentDarkColor` | `#169F85` | Accent dam |
| `BodyBgColor` | `#F7F7F7` | Nen man hinh |
| `CardBgColor` | `#FFFFFF` | Nen card |
| `BorderColor` | `#E6E9ED` | Border |
| `TextPrimaryColor` | `#3A3F44` | Text chinh |
| `TextMutedColor` | `#73879C` | Text phu |
| `SuccessColor` | `#26B99A` | Thanh cong |
| `InfoColor` | `#3498DB` | Info/in progress |
| `WarningColor` | `#F39C12` | Canh bao |
| `DangerColor` | `#E74C3C` | Loi/nguy hiem |

### 2.2 Style Controls

| Style | Dang dung cho |
| --- | --- |
| `PrimaryButton` | Hanh dong chinh: dang nhap, tao/moi, gui, refresh quan trong. |
| `SecondaryButton` | Hanh dong phu: quay lai, archive, refresh, restore, use kit quantity. |
| `SuccessButton` | Luu/hoan thanh/verify/approve. |
| `DangerButton` | Ban/cancel/delete/reject. |
| `InfoButton` | Chuyen trang thai in progress. |
| `SidebarButton` | Sidebar Buyer. |
| `SidebarNav` | Sidebar Seller/Admin dang RadioButton. |
| `DashboardCard` | Card chinh trong dashboard. |
| `KpiCard` | KPI cards. |
| `StatusBadge` / `BadgeChrome` | Badge trang thai. |
| `EmptyStateCard` | Trang thai rong cua list/grid. |
| `DataGrid` default style | Bang admin/buyer/seller/QC. |

### 2.3 Localization

Frontend dung `loc:Tr` va `Loc.Instance` de chuyen ngon ngu live.

Ngon ngu hien co:

- Tieng Viet
- English

Ghi chu khi cai tao:

- Nen giu text hien thi qua localization keys.
- Neu them man/nut moi, bo sung key vao `Localization/AppStrings.cs`.
- User menu hien co da co radio chon ngon ngu va co persistence.

## 3. Auth Va Guest Flow

### 3.1 Login Screen

File:

- `Views/LoginView.xaml`
- `ViewModels/LoginViewModel.cs`

Controls:

| Loai | Label/AutomationId | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| TextBox | `LoginEmailOrUsernameTextBox` | `EmailOrUsername` | Nhap email hoac username. |
| PasswordBox | `LoginPasswordBox` | `Password` | Nhap password. |
| TextBlock | `LoginErrorMessage` | `ErrorMessage` | Loi dang nhap. |
| TextBlock | `LoginStatusMessage` | `StatusMessage` | Trang thai sau logout/register. |
| Button | `Dang nhap` / `LoginSubmitButton` | `LoginCommand` | Dang nhap. |
| Button | `Tao tai khoan buyer` / `LoginShowRegisterButton` | `ShowRegisterCommand` | Sang man dang ky buyer. |
| Button list | Quick login accounts | `QuickLoginCommand` | Login nhanh bang account seed. |

Quick login accounts:

| Username | Role | Password mac dinh |
| --- | --- | --- |
| `buyer_refactor` | Buyer | `Password123` |
| `seller_soigear` | Seller | `Password123` |
| `admin_refactor` | Admin | `Password123` |

Use cases:

- UC 1.2: Dang nhap.
- Role routing: Buyer/Seller/Admin dashboard.
- Chan account inactive/ban.
- Hien loi credentials/account status.

### 3.2 Register Buyer Screen

File:

- `Views/RegisterView.xaml`
- `ViewModels/RegisterViewModel.cs`

Controls:

| Loai | Label/AutomationId | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| TextBox | `RegisterUsernameTextBox` | `Username` | Ten dang nhap. |
| TextBox | `RegisterEmailTextBox` | `Email` | Email. |
| TextBox | `RegisterPhoneTextBox` | `Phone` | So dien thoai. |
| PasswordBox | `RegisterPasswordBox` | `Password` | Mat khau. |
| PasswordBox | `RegisterConfirmPasswordBox` | `ConfirmPassword` | Xac nhan mat khau. |
| TextBlock | `RegisterErrorMessage` | `ErrorMessage` | Loi dang ky. |
| Button | `Dang ky` / `RegisterSubmitButton` | `RegisterCommand` | Tao buyer account. |
| Button | `Quay lai dang nhap` / `RegisterBackToLoginButton` | `ShowLoginCommand` | Quay lai login. |

Use cases:

- UC 1.1: Dang ky buyer.
- Validate confirm password.
- Service validate username/email/phone/password.
- Sau khi thanh cong quay ve login va hien status message.

## 4. User Menu Chung

File:

- `Views/UserMenuView.xaml`
- `MainShellViewModel.cs`

Controls:

| Loai | Label/AutomationId | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| ToggleButton | `UserMenuButton` | popup toggle | Mo/doi user menu. |
| ToggleButton | `Ho so tai khoan` / `UserMenuProfileButton` | local toggle | Mo panel profile. |
| Info panel | `UserMenuProfileDetails` | `CurrentUser.*` | Xem profile read-only. |
| Button | `Cai dat tai khoan` | disabled/collapsed | Chua co man account settings. |
| ToggleButton | `Tuy chon` / `UserMenuPreferencesButton` | local toggle | Mo preferences. |
| RadioButton | `Tieng Viet` / `LanguageVietnameseOption` | `Loc.Instance.Language` | Chuyen sang Tieng Viet. |
| RadioButton | `English` / `LanguageEnglishOption` | `Loc.Instance.Language` | Chuyen sang English. |
| Button | `Dang xuat` / `UserMenuLogoutButton` | `LogoutCommand` | Logout va quay ve login. |

Profile fields:

- Username
- Email
- Phone
- Role
- Status active/inactive
- User ID

Use cases:

- UC 1.3: Dang xuat.
- UC 1.4: Xem profile tai khoan.
- Chuyen ngon ngu live.

Ghi chu cai tao:

- `Account settings` da co placeholder nhung dang bi an.
- Neu them cap nhat profile/password, co the mo rong tu day.

## 5. Chat Chung

File:

- `Views/ChatView.xaml`
- `ViewModels/ChatViewModel.cs`

Visible theo role:

| Role | Co panel mo hoi thoai moi? | Chat voi |
| --- | --- | --- |
| Buyer | Co | Seller |
| Seller | Khong | Buyer hoac Admin tu conversation co san |
| Admin | Co | Seller |

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| ComboBox | Chon seller de chat | `Counterparts`, `SelectedCounterpart` | Chon seller. Chi hien voi Buyer/Admin. |
| Button | `Mo hoi thoai voi seller` | `StartConversationCommand` | Tao/mo conversation. |
| ListBox | Conversations | `Conversations`, `SelectedConversation` | Danh sach hoi thoai. |
| Button | `Lam moi hoi thoai` | `RefreshConversationsCommand` | Reload conversation list. |
| ItemsControl | Messages | `Messages` | Danh sach tin nhan. |
| TextBox | Message input | `MessageText` | Nhap noi dung tin nhan. |
| Button | `Gui` | `SendMessageCommand` | Gui tin nhan. |
| TextBlock | Status | `StatusMessage` | Trang thai chat. |

Use cases:

- UC 5.1: Buyer chat voi Seller.
- UC 5.2: Seller chat voi Buyer.
- UC 5.3: Seller chat voi Admin.
- UC 5.4: Admin chat voi Seller.

Dieu kien command:

| Command | Dieu kien |
| --- | --- |
| `StartConversationCommand` | Role Buyer/Admin, co `SelectedCounterpart`, khong busy. |
| `SendMessageCommand` | Co conversation, message khong rong, khong busy. |
| `RefreshMessagesCommand` | Co conversation, khong busy. |

Ghi chu cai tao:

- ViewModel co `RefreshMessagesCommand` nhung XAML hien khong co nut refresh messages rieng.
- Chua co unread count, typing indicator, online indicator theo docs.

## 6. Buyer Dashboard

File:

- `Views/BuyerDashboardView.xaml`
- `ViewModels/BuyerDashboardViewModel.cs`

### 6.1 Navigation

Sidebar buttons:

| Label | Command | Screen |
| --- | --- | --- |
| `Trang chu` | `ShowHomeCommand` | Home |
| `Build cua toi` | `ShowBuildListCommand` | BuildList |
| `Tao build moi` | `NewBuildCommand` | Configurator reset moi |
| `Request da gui` | `ShowSentRequestsCommand` | SentRequests |
| `Chat` | `ShowChatCommand` | Chat |
| `Dang ky Seller` | `ShowRegisterSellerCommand` | RegisterSeller |

Topbar:

| Label | Command | Mo ta |
| --- | --- | --- |
| `Lam moi` | `RefreshAllCommand` | Reload catalog, builds, sellers, requests, seller application. |
| User menu | `UserMenuView` | Profile/language/logout. |

Ghi chu use case:

- Buyer co 7 entry point tong quat tren UI: `User menu` cho quan ly tai khoan, va 6 muc sidebar `Trang chu`, `Build cua toi`, `Tao build moi`, `Request da gui`, `Chat`, `Dang ky Seller`.
- Cac thao tac chon kit, them linh kien, xem canh bao, xem tong gia, luu build, chon seller va gui request la luong con trong entry point `Tao build moi`.
- Xem tom tat QC la luong con read-only trong `Request da gui`; Buyer khong thao tac QC tung phim.

### 6.2 Home

Noi dung:

- Hien username/role.
- Hien workflow buyer.

Workflow hien tai:

- Chon kit va them switch/keycap/stabilizer/accessory.
- Xem canh bao tuong thich va tong gia.
- Luu build va gui request cho seller.
- Theo doi trang thai request va chat.

### 6.3 My Builds

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| Button | `+ Tao build moi` | `NewBuildCommand` | Reset va mo configurator. |
| DataGrid | Buyer builds | `Builds`, `SelectedBuild` | Danh sach build. |
| Button | `Mo trong configurator` | `OpenBuildCommand` | Load build da chon vao configurator. |
| Button | `Luu tru` | `ArchiveBuildCommand` | Archive build da chon. |

DataGrid columns:

- Build name
- Kit
- Status
- Total USD

Empty state:

- Chua co build nao.
- Goi y tao build moi.

Dieu kien command:

| Command | Dieu kien |
| --- | --- |
| `OpenBuildCommand` | Khong busy. Neu chua chon build thi hien status message. |
| `ArchiveBuildCommand` | Co `SelectedBuild`, khong busy. |

Use cases:

- UC-01 / Build cua toi: gom UC 2.2 Xem danh sach build va UC 2.12 Luu tru build.
- UC 2.3 Tao build moi co the duoc mo tu sidebar hoac tu nut trong danh sach build, nhung flow cau hinh nam o entry point `Tao build moi`.

### 6.4 Build Configurator

Controls chinh:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| TextBox | Ten build | `BuildName` | Bat buoc de save. |
| TextBox | Ghi chu | `BuildNotes` | Optional. |
| ComboBox | Yeu cau do on | `NoiseRequirements`, `SelectedNoiseRequirement` | Normal/quiet requirement. |
| ComboBox | Kit | `Kits`, `SelectedKit` | Chon keyboard kit. |
| TextBlock | Kit summary | `KitSummary` | PCB, mount, required switches, included parts. |
| ComboBox | Switch | `CompatibleSwitches`, `SelectedSwitch` | Chi hien switch compatible voi kit. |
| Button | `Dung so luong kit` | `ApplyRequiredSwitchQuantityCommand` | Set quantity = kit required quantity. |
| TextBox | So luong switch | `SwitchQuantity` | So switch mua/mod. |
| ComboBox | Keycap | `Keycaps`, `SelectedKeycap` | Chon keycap set. |
| ComboBox | Stabilizer | `Stabilizers`, `SelectedStabilizer` | Chon stabilizer. |
| CheckBox list | Accessory | `Accessories[].IsSelected` | Chon phu kien. |

Mod controls:

| Loai | Label | Command/Binding | Mo ta |
| --- | --- | --- | --- |
| Button | `Xoa mod dang chon` | `RemoveModCommand` | Xoa mod selected. |
| Button | `Lube switch` | `AddModCommand`, `Switch|Lube` | Them switch lube. |
| Button | `Film switch` | `AddModCommand`, `Switch|Film` | Them switch film. |
| Button | `Thay spring` | `AddModCommand`, `Switch|Spring_swap` | Them spring swap. |
| Button | `Lube stab` | `AddModCommand`, `Stabilizer|Lube` | Them stab lube. |
| Button | `Tune stab` | `AddModCommand`, `Stabilizer|Tune` | Them stab tune. |
| Button | `Tape mod` | `AddModCommand`, `Build|Tape_mod` | Them tape mod. |
| Button | `Foam mod` | `AddModCommand`, `Build|Foam_mod` | Them foam mod. |
| ListBox | Mods | `Mods`, `SelectedMod` | Danh sach mod editor. |

Mod editor row:

| Control | Binding | Mo ta |
| --- | --- | --- |
| ComboBox | `TargetComponent` | Switch/Stabilizer/Build. |
| ComboBox | `ModType` | Phu thuoc target. |
| ComboBox | `ModQuantity` | So switch ap dung, chi hien voi target Switch. |
| ComboBox | `SpringWeightG` | Gram 30-76g, chi hien voi Spring swap. |
| TextBox | `Notes` | Ghi chu optional. |

Right summary panel:

| Thanh phan | Binding |
| --- | --- |
| Total price snapshot | `TotalPreview` |
| Error list | `Errors` |
| Warning list | `Warnings` |
| Info list | `Infos` |
| Button `Luu build` | `SaveBuildCommand` |

Send request panel:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| ComboBox | Seller | `AvailableSellers`, `SelectedSeller` | Chi seller active/verified. |
| TextBlock | Seller empty message | `SellerEmptyMessage` | Bao khong co seller hop le. |
| Seller stats card | `SellerStats` | Reputation public, khong co doanh thu. |
| TextBox | Request note | `RequestNote` | Note gui seller. |
| Button | `Gui request` | `SendRequestCommand` | Tao build request. |

Dieu kien command:

| Command | Dieu kien |
| --- | --- |
| `ApplyRequiredSwitchQuantityCommand` | Co `SelectedKit`. |
| `RemoveModCommand` | Co `SelectedMod`. |
| `SaveBuildCommand` | Build valid, co ten build, co selected kit, khong busy. |
| `SendRequestCommand` | Co `SelectedBuild`, co `SelectedSeller`, khong busy. |

Use cases:

- UC-01 / Tao build moi: gom UC 2.3-2.10.
- UC 2.3-2.8: Tao/cau hinh/validate/tinh gia/luu build.
- UC 2.9-2.10: Chon seller verified va gui request la action mo rong sau khi build da luu.

Ghi chu nghiep vu:

- Kit quyet dinh layout, PCB technology, switch mount, required switch quantity.
- Compatible switches duoc filter theo `SwitchTechnology` va `MountType`.
- Validation chay moi khi doi kit/switch/keycap/stabilizer/accessory/mod.
- Total preview la snapshot USD.

### 6.5 Sent Requests

Controls:

| Loai | Binding | Mo ta |
| --- | --- | --- |
| DataGrid | `Requests`, `SelectedRequest` | Request da gui. |
| QC summary panel | `SelectedRequestQc` | Tom tat QC moi nhat cua request selected. |

DataGrid columns:

- Request ID
- Build ID
- Seller User ID
- Status

QC summary read-only:

- Result/status
- Passed keys / total keys
- Average latency
- Average noise
- No data message neu chua co QC

Use cases:

- UC-01 / Request da gui: gom UC 2.11 Theo doi request va UC 6.7 Xem tom tat QC read-only.

Ghi chu cai tao:

- Buyer chi xem summary, khong xem per-key detail.

### 6.6 Register Seller

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| Status panel | Current application | `MyApplication`, `MyApplicationStatusText` | Hien trang thai don seller. |
| TextBox | Ten shop | `SellerAppShopName` | Bat buoc. |
| TextBox | Phone | `SellerAppPhone` | Bat buoc. |
| TextBox | Address | `SellerAppAddress` | Bat buoc. |
| TextBox | Ghi chu optional | `SellerAppNote` | Optional. |
| Button | `Gui don dang ky` | `SubmitSellerApplicationCommand` | Nop don seller. |

Dieu kien command:

| Command | Dieu kien |
| --- | --- |
| `SubmitSellerApplicationCommand` | Khong busy, khong co don Pending, shop/phone/address khong rong. |

Use cases:

- UC-01 / Dang ky Seller: map vao UC 2.13 Buyer dang ky tro thanh seller.

## 7. Seller Dashboard

File:

- `Views/SellerDashboardView.xaml`
- `ViewModels/SellerDashboardViewModel.cs`

### 7.1 Navigation

Sidebar RadioButtons:

| Label | Element | Mo ta |
| --- | --- | --- |
| `Request` | `NavRequest` | Danh sach request va snapshot. |
| `Kiem tra QC` | `NavQc` | Chay QC va xem ket qua tung phim. |
| `Phan tich` | `NavAnalytics` | KPI va chart seller. |
| `Chat` | `NavChat` | Chat panel. |

Topbar:

| Label | Command |
| --- | --- |
| `Lam moi` | `RefreshCommand` |
| User menu | `LogoutCommand` qua menu |

Ghi chu use case:

- Seller co 5 entry point tong quat tren UI: `User menu` cho quan ly tai khoan, va 4 muc sidebar `Request`, `Kiem tra QC`, `Phan tich`, `Chat`.
- Cac nut `Chap nhan`, `Dang lam`, `Hoan thanh`, `Huy` la luong con trong use case `Xu ly request duoc gan`, khong phai use case cap sidebar rieng.

### 7.2 Request Tab

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| DataGrid | Assigned requests | `Requests`, `SelectedRequest` | Request duoc gan cho seller. |
| Button | `Chap nhan` | `AcceptCommand` | Pending -> Accepted. |
| Button | `Dang lam` | `StartProgressCommand` | Accepted -> In_progress. |
| Button | `Hoan thanh` | `CompleteCommand` | In_progress -> Completed neu QC dat. |
| Button | `Huy` | `CancelCommand` | Pending/Accepted/In_progress -> Cancelled. |
| TextBox read-only | Snapshot build | `SelectedRequestPayload` | Chi tiet build snapshot. |

DataGrid columns:

- Request ID
- Build ID
- Requested date
- Status

State rules:

| Trang thai hien tai | Nut hop le |
| --- | --- |
| Pending | Chap nhan, Huy |
| Accepted | Dang lam, Huy |
| In_progress | Huy, Hoan thanh neu QC Passed/Warning |
| Completed/Cancelled | Khong co transition |

Use cases:

- UC-02 / Xu ly request duoc gan: gom UC 3.2-3.7.
- UC 3.2: Xem danh sach request.
- UC 3.3: Xem chi tiet request.
- UC 3.4-3.7: Chap nhan, cap nhat dang xu ly, hoan thanh, huy la cac action ben trong request tab.

### 7.3 QC Test Tab

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| Button | `Bat dau kiem tra QC` | `StartQcTestCommand` | Chay simulator QC cho request selected. |
| Summary panel | QC summary | `QcSession` | Tong ket session. |
| DataGrid | Per-key results | `KeyResults` | Ket qua tung phim. |

Dieu kien:

| Command | Dieu kien |
| --- | --- |
| `StartQcTestCommand` | Co request selected dang `In_progress`, khong busy. |

QC summary:

- Status
- Tested/total
- Passed
- Warning
- Failed
- Average latency
- Max latency
- Average noise
- Max noise

Per-key DataGrid columns:

- Key
- Press detected
- Release detected
- Press events
- Bounce
- Latency
- Stuck
- Result
- Failure type
- Reason

Use cases:

- UC-02 / Kiem tra QC keyboard: gom UC 3.8-3.10.
- UC 3.8: Bat dau QC test.
- UC 3.9: Xem ket qua QC tung phim.
- UC 3.10: Xac nhan hoan thanh sau QC, lien quan nut Hoan thanh trong Request tab.
- UC 6.1-6.7: Device/QC station simulation.

### 7.4 Analytics Tab

Controls:

| Loai | Binding | Mo ta |
| --- | --- | --- |
| KPI cards | `TotalRevenue`, `ProductsMade`, `TotalCustomers`, `InProgressOrders`, `AvgCompletionText` | Tong quan seller. |
| ComboBox | `Periods`, `SelectedPeriod` | Doi ky thong ke. |
| CartesianChart | `RevenueSeries`, `RevenueXAxes`, `RevenueYAxes` | Doanh thu va so don. |
| PieChart | `StatusSeries` | Ti le trang thai. |
| ItemsControl | `TopKits` | Top kit. |

Use cases:

- UC-02 / Phan tich seller: map vao UC 3.1 Xem dashboard seller/KPI.
- Theo doi analytics seller.

### 7.5 Chat Tab

Dung `ChatView` voi DataContext `Chat`.

Use cases:

- UC-02 / Chat: gom UC 5.2 Seller chat voi Buyer va UC 5.3 Seller chat voi Admin.

## 8. Admin Dashboard

File:

- `Views/AdminDashboardView.xaml`
- `ViewModels/AdminDashboardViewModel.cs`

### 8.1 Navigation

Sidebar RadioButtons:

| Label | Element | Mo ta |
| --- | --- | --- |
| `Tong quan` | `NavOverview` | Analytics toan san. |
| `Nguoi dung` | `NavUser` | Quan ly user/role/ban. |
| `Seller` | `NavSeller` | Quan ly seller profile/verify. |
| `Don xin Seller` | `NavApplications` | Duyet/tuchoi don seller. |
| `Brand` | `NavBrand` | Quan ly brand. |
| `Linh kien` | `NavComponent` | Quan ly component catalog. |
| `Audit log` | `NavAudit` | Xem audit log. |
| `Chat` | `NavChat` | Chat voi seller. |

Topbar:

| Label | Command |
| --- | --- |
| `Lam moi` | `RefreshAllCommand` |
| User menu | `LogoutCommand` qua menu |

KPI chung luon hien:

- Users
- Sellers
- Components
- Requests

Ghi chu use case:

- Admin co 9 entry point tong quat tren UI: `User menu` cho quan ly tai khoan, va 8 tab sidebar `Tong quan`, `Nguoi dung`, `Seller`, `Don xin Seller`, `Brand`, `Linh kien`, `Audit log`, `Chat`.
- FHD 4.4 `Quan ly catalog` duoc tach theo UI thanh 2 entry point `Brand` va `Linh kien`.
- Cac nut chi tiet nhu doi role, khoa user, verify seller, approve/reject don, an/khoi phuc linh kien la luong con trong tab tuong ung.

### 8.2 Overview

Controls:

| Loai | Binding | Mo ta |
| --- | --- | --- |
| KPI cards | `TotalRevenue`, `CompletedOrders`, `VerifiedSellers`, `TotalBuilds`, `TotalRequests` | Toan san. |
| ComboBox | `Periods`, `SelectedPeriod` | Ky thong ke. |
| Text summary | `AnalyticsTotalUsers`, `UsersByRoleText` | User by role. |
| CartesianChart | `RevenueSeries` | Doanh thu theo thoi gian. |
| PieChart | `StatusSeries` | Don theo trang thai. |
| CartesianChart | `TopSellersSeries` | Top seller theo doanh thu. |

Use cases:

- UC 4.1: Xem dashboard admin.

### 8.3 Users

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| DataGrid | Users | `Users`, `SelectedUser` | Danh sach user. |
| ComboBox | Role | `AvailableRoles`, `SelectedUserRole` | Chon role moi. |
| Button | `Doi vai tro` | `ChangeUserRoleCommand` | Cap nhat role user. |
| Button | `Khoa user` | `BanUserCommand` | Set inactive. |
| Button | `Mo khoa` | `UnbanUserCommand` | Set active. |

DataGrid columns:

- Username
- Email
- Role
- Status active/banned

Use cases:

- UC 4.2: Quan ly user.

Service-level guard dang co:

- Admin khong the lock chinh minh.
- Admin khong the doi role chinh minh khoi Admin.

### 8.4 Sellers

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| DataGrid | Seller profiles | `SellerProfiles`, `SelectedSellerProfile` | Danh sach seller. |
| TextBox | Ten shop | `SellerShopName` | Editor profile. |
| TextBox | Phone | `SellerPhone` | Editor profile. |
| TextBox | Address | `SellerAddress` | Editor profile. |
| Button | `Luu ho so` | `SaveSellerProfileCommand` | Luu seller profile. |
| Button | `Verify` | `VerifySellerCommand` | Verify seller. |
| Button | `Unverify` | `UnverifySellerCommand` | Bo verify seller. |

DataGrid columns:

- Username
- Shop
- Verify status

Use cases:

- UC 4.3: Quan ly seller profile.
- Seller chi duoc buyer chon khi active, role Seller va verified.

### 8.5 Seller Applications

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| Button | `Lam moi` | `RefreshApplicationsCommand` | Reload don seller. |
| DataGrid | Applications | `Applications`, `SelectedApplication` | Danh sach don. |
| Detail panel | Application detail | `SelectedApplication` | Applicant/email/shop/phone/address/note. |
| TextBox | Review note optional | `ApplicationReviewNote` | Ghi chu duyet/tu choi. |
| Button | `Chap nhan` | `ApproveApplicationCommand` | Duyet don. |
| Button | `Tu choi` | `RejectApplicationCommand` | Tu choi don. |

DataGrid columns:

- Applicant
- Shop
- Date
- Status

Dieu kien:

| Command | Dieu kien |
| --- | --- |
| `ApproveApplicationCommand` | Co application selected, status Pending, khong busy. |
| `RejectApplicationCommand` | Co application selected, status Pending, khong busy. |

Use cases:

- UC 4.6: Duyet don xin lam seller.

### 8.6 Brand

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| ListBox | Brands | `Brands`, `SelectedBrand` | Danh sach brand. |
| TextBox | Ten brand | `BrandEditor.BrandName` | Editor. |
| TextBox | Quoc gia | `BrandEditor.Country` | Editor. |
| Button | `Moi` | `NewBrandCommand` | Reset editor. |
| Button | `Luu brand` | `SaveBrandCommand` | Tao/cap nhat brand. |
| Button | `Xoa brand` | `DeleteBrandCommand` | Xoa brand neu khong in-use. |

Dieu kien:

| Command | Dieu kien |
| --- | --- |
| `DeleteBrandCommand` | Co selected brand id > 0, khong busy. |

Use cases:

- UC 4.4: Quan ly catalog, brand.

Ghi chu:

- Navigation label la `Brand`, trong key co ten `Admin_NavBrandLayout`.
- ViewModel co support `Layout` command/editor, nhung XAML hien tai chua co UI quan ly layout rieng.

### 8.7 Components

Controls chinh:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| ComboBox | Component type | `ComponentTypes`, `SelectedComponentType` | Kit/Switch/KeycapSet/Stabilizer/Accessory. |
| DataGrid | Components | `Components`, `SelectedComponent` | Danh sach component theo type. |
| Button | `An` | `HideComponentCommand` | Soft hide selected component. |
| Button | `Khoi phuc` | `RestoreComponentCommand` | Restore selected component. |
| Editor fields | `ComponentEditor.*` | Theo component type | Tao/cap nhat component. |
| Button | `Moi` | `NewComponentCommand` | Reset editor. |
| Button | `Luu linh kien` | `SaveComponentCommand` | Tao/cap nhat component. |

DataGrid columns:

- Name
- ID
- Price USD
- Available/Hidden

Common editor fields:

- Component ID
- Name
- Price USD
- Available checkbox

Brand field:

- Hien voi Kit, Switch, KeycapSet, Stabilizer.
- An voi Accessory.

Kit fields:

- Brand
- Layout
- PCB technology
- Switch mount
- Required switch quantity
- Included parts

Switch fields:

- Brand
- Switch technology
- Mount type
- Switch type
- Actuation force

Keycap fields:

- Brand
- Supported form factor
- Profile
- Material

Stabilizer fields:

- Brand
- Supported layouts

Accessory fields:

- Accessory type
- Target component

Use cases:

- UC 4.4: Quan ly catalog.
- Soft delete bang `is_available`.

### 8.8 Audit Log

Controls:

| Loai | Label | Binding/Command | Mo ta |
| --- | --- | --- | --- |
| Button | `Lam moi audit` | `RefreshAuditCommand` | Reload audit logs. |
| DataGrid | Audit logs | `AuditLogs` | Xem lich su thao tac. |

DataGrid columns:

- Action
- Table
- Record
- Time

Use cases:

- UC 4.5: Xem audit log.

### 8.9 Admin Chat

Dung `ChatView` voi DataContext `Chat`.

Use cases:

- UC 5.4: Admin chat voi seller.

## 9. Device/QC Use Cases

Device/QC layer la simulation, khong phai hardware that trong phase hien tai.

Actor:

- Seller
- Device Simulator

Frontend surfaces:

| Role | Man hinh | Noi dung |
| --- | --- | --- |
| Seller | QC tab | Start QC, xem summary, xem per-key results. |
| Buyer | Sent Requests | Xem QC summary read-only cho request selected. |

Luong chinh:

1. Seller chon request dang `In_progress`.
2. Seller mo tab `Kiem tra QC`.
3. Seller bam `Bat dau kiem tra QC`.
4. App tao/lay QC station va session.
5. Simulator sinh telemetry tung phim.
6. App luu ket qua tung phim.
7. App tong hop session status: Passed/Warning/Failed.
8. Seller xem per-key detail.
9. Buyer xem summary khi mo sent request.
10. Seller chi complete request khi QC Passed hoac Warning.

Per-key failure co the hien:

- NoSignal
- WrongKey
- Chatter
- StuckKey
- HighLatency
- TooNoisy

## 10. Use Case Mapping Tong Hop

### 10.1 Account

| Ma | Use case | Frontend surface |
| --- | --- | --- |
| 1.1 | Dang ky | Register screen |
| 1.2 | Dang nhap | Login screen |
| 1.3 | Dang xuat | User menu |
| 1.4 | Xem profile tai khoan | User menu profile panel |

### 10.2 Buyer

| Entry point tong quat | Ma FHD / action ben trong | Frontend surface |
| --- | --- | --- |
| Dang ky tai khoan Buyer | 1.1 | Register screen |
| Quan ly tai khoan | 1.2, 1.3, 1.4 | Login screen + user menu |
| Trang chu Buyer | 2.1 | Buyer home |
| Build cua toi | 2.2; action con 2.12 Luu tru build | My Builds grid, open/archive buttons |
| Tao build moi | 2.3-2.8; action mo rong 2.9 Chon seller va 2.10 Gui request | Configurator, seller selector, send request panel |
| Request da gui | 2.11; QC summary 6.7 read-only | Sent Requests grid + QC summary |
| Chat | 5.1 | Chat tab |
| Dang ky Seller | 2.13 | Become a seller screen |

### 10.3 Seller

| Entry point tong quat | Ma FHD / action ben trong | Frontend surface |
| --- | --- | --- |
| Quan ly tai khoan | 1.2, 1.3, 1.4 | Login screen + user menu |
| Xu ly request duoc gan | 3.2, 3.3; action con 3.4 Chap nhan, 3.5 Dang lam, 3.6 Hoan thanh, 3.7 Huy | Request tab, request grid, snapshot panel, status buttons |
| Kiem tra QC keyboard | 3.8, 3.9, 3.10; ho tro 6.1-6.7 | QC tab, QC start button, summary, per-key grid |
| Phan tich seller | 3.1 | Phan tich tab / seller KPI charts |
| Chat | 5.2, 5.3 | Chat tab |

### 10.4 Admin

| Entry point tong quat | Ma FHD / action ben trong | Frontend surface |
| --- | --- | --- |
| Quan ly tai khoan | 1.2, 1.3, 1.4 | Login screen + user menu |
| Tong quan | 4.1 | Overview tab |
| Nguoi dung | 4.2; action con doi role/khoa/mo khoa | Users tab |
| Seller | 4.3; action con luu profile/verify/unverify | Sellers tab |
| Don xin Seller | 4.6; approve co the cap nhat 4.3 | Seller applications tab |
| Brand | 4.4 | Brand tab |
| Linh kien | 4.4 | Components tab |
| Audit log | 4.5 | Audit log tab |
| Chat | 5.4 | Chat tab |

### 10.5 Device/QC

| Ma | Use case | Frontend surface |
| --- | --- | --- |
| 6.1 | Tao/lay tram QC | Triggered by seller Start QC |
| 6.2 | Bat dau phien QC | Seller QC tab |
| 6.3 | Kiem tra tin hieu phim | Simulator/backend, result shown in per-key grid |
| 6.4 | Kiem tra latency | Per-key grid + summary |
| 6.5 | Kiem tra do on | Per-key grid + summary |
| 6.6 | Xem ket qua tung phim | Seller QC tab |
| 6.7 | Tong hop ket qua QC | Seller summary, Buyer summary |

## 11. AutomationId Va UI Test Coverage

AutomationId dang co:

| Area | AutomationId |
| --- | --- |
| Login | `LoginEmailOrUsernameTextBox`, `LoginPasswordBox`, `LoginErrorMessage`, `LoginStatusMessage`, `LoginSubmitButton`, `LoginShowRegisterButton` |
| Register | `RegisterUsernameTextBox`, `RegisterEmailTextBox`, `RegisterPhoneTextBox`, `RegisterPasswordBox`, `RegisterConfirmPasswordBox`, `RegisterErrorMessage`, `RegisterSubmitButton`, `RegisterBackToLoginButton` |
| Buyer | `BuyerDashboardRoot`, `BuyerQcSummaryPanel`, `BuyerQcSummaryResultLabel`, `BuyerQcNoDataMessage` |
| Seller | `SellerDashboardRoot`, `SellerQcSummaryPanel`, `SellerQcSummaryTitle`, `SellerQcTestedCount` |
| Admin | `AdminDashboardRoot` |
| User menu | `UserMenuButton`, `UserMenuProfileButton`, `UserMenuProfileDetails`, `UserMenuPreferencesButton`, `UserMenuLanguagePanel`, `LanguageVietnameseOption`, `LanguageEnglishOption`, `UserMenuLogoutButton` |

UI verification hien co:

- Login smoke cho Buyer/Seller/Admin.
- User menu profile.
- Live language switch + persistence.
- Seller analytics + chart localization.
- Admin tab navigation.
- Seller request snapshot.
- Seller run QC and buyer sees summary.

Ghi chu can xem lai:

- UI test tim `BuyerQcSummaryStatusLabel` trong `WpfUiVerification`, nhung XAML hien co `BuyerQcSummaryResultLabel`.
- Nhieu nut quan trong chua co AutomationId, nen them khi redesign de test on dinh hon.

## 12. Cac Diem Can Chu Y Khi Cai Tao Frontend

### 12.1 Navigation Pattern Chua Dong Nhat

| Role | Pattern hien tai |
| --- | --- |
| Buyer | Button command + `CurrentScreen` trong ViewModel. |
| Seller | RadioButton pure UI + visibility binding `IsChecked`. |
| Admin | RadioButton pure UI + visibility binding `IsChecked`. |

Neu cai tao lon, nen can nhac thong nhat pattern navigation de:

- De test hon.
- De quan ly selected state tot hon.
- De luu route/current tab neu can.

### 12.2 Layout Manager Chua Co UI Day Du

`AdminDashboardViewModel` co:

- `SelectedLayout`
- `LayoutEditor`
- `NewLayoutCommand`
- `SaveLayoutCommand`
- `RefreshLayoutsAsync`

Localization cung co labels cho Layout. Tuy nhien XAML hien tai chi co tab `Brand`, chua render UI quan ly layout rieng.

Neu cai tao catalog admin, nen quyet dinh:

- Tach `Brand` va `Layout` thanh 2 tab/section.
- Hoac giu mot tab `Brand & Layout` dung split view.

### 12.3 Command Co San Nhung UI Chua Dung Het

Admin ViewModel co cac command refresh rieng:

- `RefreshUsersCommand`
- `RefreshSellersCommand`
- `RefreshCatalogCommand`
- `RefreshComponentsCommand`

UI hien tai chu yeu dung:

- `RefreshAllCommand`
- `RefreshAuditCommand`
- `RefreshApplicationsCommand`

Neu redesign, co the them refresh local theo tab hoac bo command khong dung.

### 12.4 Chat Chua Co Advanced UX

Chua co:

- Unread count.
- Typing indicator.
- Online/offline real presence.
- Attachment.
- Search conversation.
- Message refresh button visible.

Neu chat la trong tam, can cai tao thanh split pane ro rang hon.

### 12.5 Buyer Configurator La Man Hinh Phuc Tap Nhat

Cac khu vuc co nhieu state:

- Component selection.
- Compatibility validation.
- Mod presets/editor.
- Total price snapshot.
- Seller selection + seller stats.
- Request note/send request.

Khi cai tao UI, nen uu tien:

- Giam scroll doc dai.
- Gom theo stepper hoac sections.
- Tach Save build va Send request de tranh nham lan.
- Hien state "build chua du dieu kien luu/gui" ro hon.

### 12.6 Seller Complete Bi Rang Buoc Boi QC

Nut `Hoan thanh` khong chi la status transition binh thuong. No phu thuoc:

- Request dang `In_progress`.
- QC result `Passed` hoac `Warning`.

UI moi nen the hien ly do disabled neu chua dat dieu kien QC.

### 12.7 Admin Component Editor Co Nhieu Field Dieu Kien

Editor component thay doi theo type. Neu cai tao:

- Can co field group ro rang.
- Can co validation inline.
- Can co empty/edit/create state ro.
- Can can nhac type-specific form component.

### 12.8 Responsive Desktop

App hien la desktop-first WPF, min width 980. Khi cai tao:

- Chu y DataGrid columns co the bi chat.
- Sidebar 240px co dinh.
- Card nested va grid 2-3 cot can kiem tra o min size.
- Buyer configurator co cot right 320px, co the chat tai min width.

## 13. Checklist Cai Tao De Xuat

### 13.1 Nen Tang

- Giu localization cho tat ca text.
- Them AutomationId cho nav/nut quan trong.
- Thong nhat navigation pattern.
- Chuan hoa empty/loading/error states.
- Hien disabled reason cho nut nghiep vu quan trong.

### 13.2 Buyer

- Cai tao configurator thanh flow ro rang: Build info -> Parts -> Mods -> Review -> Seller/request.
- Hien validation gan field thay vi chi o panel tong.
- Lam ro selected saved build vs build dang edit.
- Lam ro "Save build" phai thanh cong truoc khi "Send request".
- Seller selection nen co cards/metadata de so sanh.
- Sent requests nen co detail drawer/panel ro hon.

### 13.3 Seller

- Request tab nen co detail panel co cau truc thay vi snapshot text dai.
- Status action nen hien theo state machine.
- QC tab nen lien ket request selected ro rang.
- Complete button nen co tooltip/notice neu bi chan do chua QC.
- Analytics nen co period switch ro va empty chart state.

### 13.4 Admin

- Tach ro Overview, User management, Seller profiles, Seller applications, Catalog, Audit.
- Components editor nen co type-specific form.
- Them Layout management UI neu tiep tuc dung layout trong catalog.
- Actions nguy hiem nhu ban/delete/reject nen can confirm neu scope production.
- Audit log nen co filter/search neu du lieu nhieu.

### 13.5 Chat/User Menu

- Them refresh messages hoac auto refresh/realtime neu can.
- Can nhac unread count.
- Account settings placeholder nen duoc quyet dinh: bo an hoac trien khai.
- User menu nen giu profile compact va language switch.

## 14. Danh Sach Nut Chuc Nang Nhanh

### Auth

- Dang nhap
- Tao tai khoan buyer
- Quick login buyer/seller/admin
- Dang ky
- Quay lai dang nhap

### User Menu

- Mo user menu
- Ho so tai khoan
- Tuy chon
- Tieng Viet
- English
- Dang xuat

### Chat

- Mo hoi thoai voi seller
- Lam moi hoi thoai
- Gui

### Buyer

- Trang chu
- Build cua toi
- Tao build moi
- Request da gui
- Chat
- Dang ky Seller
- Lam moi
- + Tao build moi
- Mo trong configurator
- Luu tru
- Dung so luong kit
- Xoa mod dang chon
- Lube switch
- Film switch
- Thay spring
- Lube stab
- Tune stab
- Tape mod
- Foam mod
- Luu build
- Gui request
- Gui don dang ky

### Seller

- Request
- Kiem tra QC
- Phan tich
- Chat
- Lam moi
- Chap nhan
- Dang lam
- Hoan thanh
- Huy
- Bat dau kiem tra QC

### Admin

- Tong quan
- Nguoi dung
- Seller
- Don xin Seller
- Brand
- Linh kien
- Audit log
- Chat
- Lam moi
- Doi vai tro
- Khoa user
- Mo khoa
- Luu ho so
- Verify
- Unverify
- Moi brand
- Luu brand
- Xoa brand
- An component
- Khoi phuc component
- Moi component
- Luu linh kien
- Lam moi audit
- Lam moi applications
- Chap nhan don seller
- Tu choi don seller
