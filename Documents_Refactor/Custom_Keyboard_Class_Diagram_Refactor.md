# Custom Keyboard Builder - Class Diagram Refactor

Tai lieu nay la class diagram muc thiet ke code cho refactor theo ERD moi trong `Documents_Refactor`.

Muc tieu cua diagram:

- Lam ro cac lop domain can co sau refactor.
- Lam ro service/repository interface can sua.
- Tranh giu sot model cu: `KeyboardCase`, `Pcb`, `Plate`, `CompatibilityRule`.
- Phu hop voi flow moi: Buyer chon `KeyboardKit`, sau do them switch/keycap/stabilizer/accessory vao `BuildItem`.

## Nguyen Tac Refactor

| Cu | Moi |
| --- | --- |
| Build chon `Layout`, `Case`, `PCB`, `Plate` rieng. | Build chon `KeyboardKit`; kit da gom case/PCB/plate o muc mo ta. |
| Build luu `switch_id`, `keycap_id`, `stab_id` tren bang builds. | Build luu cac add-on qua `BuildItem`. |
| `switch_quantity` nam tren build. | Quantity cua switch nam tren `BuildItem.Quantity`. |
| Compatibility rule case/PCB/plate. | Kiem tra tuong thich theo kit va build items. |
| Inventory/seller price. | Khong co trong phase refactor nay. |

## Domain Model Class Diagram

```mermaid
classDiagram
    direction LR

    class Role {
        +int RoleId
        +string RoleName
        +string? Permissions
    }

    class User {
        +int UserId
        +int RoleId
        +string Username
        +string Email
        +string Phone
        +string PasswordHash
        +bool IsActive
    }

    class SellerProfile {
        +int SellerProfileId
        +int UserId
        +string ShopName
        +string Phone
        +string Address
        +bool IsVerified
        +DateTime? VerifiedAt
    }

    class Brand {
        +int BrandId
        +string BrandName
        +string? Country
    }

    class Layout {
        +string LayoutId
        +string LayoutName
        +string FormFactor
        +int KeyCount
    }

    class CatalogItem {
        <<abstract>>
        +int BrandId
        +decimal PriceUsd
        +bool IsAvailable
    }

    class KeyboardKit {
        +string KitId
        +string LayoutId
        +string KitName
        +string PcbTechnology
        +string SwitchMount
        +int RequiredSwitchQuantity
        +string? IncludedParts
    }

    class KeyboardSwitch {
        +string SwitchId
        +string SwitchName
        +string SwitchTechnology
        +string MountType
        +string? SwitchType
        +int? ActuationForceG
    }

    class KeycapSet {
        +string KeycapId
        +string KeycapName
        +string SupportedFormFactor
        +string? Profile
        +string? Material
    }

    class Stabilizer {
        +string StabilizerId
        +string StabilizerName
        +string SupportedLayouts
    }

    class Accessory {
        +string AccessoryId
        +string AccessoryType
        +string AccessoryName
        +string? TargetComponent
        +decimal PriceUsd
        +bool IsAvailable
    }

    class KeyboardBuild {
        +string BuildId
        +int BuyerId
        +string KitId
        +string Name
        +string? Notes
        +BuildStatus Status
        +decimal TotalCostSnapshot
        +DateTime CreatedAt
        +DateTime? UpdatedAt
        +List~BuildItem~ Items
        +List~BuildMod~ Mods
    }

    class BuildItem {
        +int BuildItemId
        +string BuildId
        +string? SwitchId
        +string? KeycapId
        +string? StabilizerId
        +string? AccessoryId
        +int Quantity
        +decimal UnitPriceSnapshot
        +string? Notes
        +bool HasExactlyOneProduct()
    }

    class BuildMod {
        +int ModId
        +string BuildId
        +string ModType
        +string TargetComponent
        +string? Notes
    }

    class BuildRequest {
        +string RequestId
        +string BuildId
        +int SellerUserId
        +string RequestPayloadJson
        +RequestStatus Status
        +string? Note
        +DateTime RequestedAt
        +DateTime? AcceptedAt
        +DateTime? CompletedAt
        +DateTime? UpdatedAt
    }

    class AuditLogEntry {
        +int LogId
        +int UserId
        +string TableName
        +string RecordId
        +string Action
        +string? OldValueJson
        +string? NewValueJson
        +DateTime ChangedAt
    }

    class ChatConversation {
        +string ConversationId
        +int SellerUserId
        +int? BuyerId
        +int? AdminUserId
        +string? BuildRequestId
        +DateTime CreatedAt
        +DateTime? UpdatedAt
        +bool HasValidParticipants()
    }

    class ChatMessage {
        +string MessageId
        +string ConversationId
        +int SenderUserId
        +string MessageText
        +DateTime SentAt
    }

    Role "1" --> "0..*" User : has
    User "1" --> "0..1" SellerProfile : sellerProfile
    User "1" --> "0..*" KeyboardBuild : buyerBuilds
    User "1" --> "0..*" BuildRequest : sellerRequests
    User "1" --> "0..*" AuditLogEntry : creates

    Brand "1" --> "0..*" KeyboardKit : brands
    Brand "1" --> "0..*" KeyboardSwitch : brands
    Brand "1" --> "0..*" KeycapSet : brands
    Brand "1" --> "0..*" Stabilizer : brands

    CatalogItem <|-- KeyboardKit
    CatalogItem <|-- KeyboardSwitch
    CatalogItem <|-- KeycapSet
    CatalogItem <|-- Stabilizer

    Layout "1" --> "0..*" KeyboardKit : supports
    KeyboardKit "1" --> "0..*" KeyboardBuild : selectedBy
    KeyboardBuild "1" o-- "0..*" BuildItem : items
    KeyboardBuild "1" o-- "0..*" BuildMod : mods
    KeyboardBuild "1" --> "0..*" BuildRequest : requests

    BuildItem "0..*" --> "0..1" KeyboardSwitch : switch
    BuildItem "0..*" --> "0..1" KeycapSet : keycap
    BuildItem "0..*" --> "0..1" Stabilizer : stabilizer
    BuildItem "0..*" --> "0..1" Accessory : accessory

    BuildRequest "0..*" --> "1" KeyboardBuild : build
    ChatConversation "1" o-- "0..*" ChatMessage : messages
    ChatConversation "0..*" --> "0..1" BuildRequest : request
```

## Service Va Repository Class Diagram

```mermaid
classDiagram
    direction LR

    class IAccountService {
        <<interface>>
        +RegisterAsync()
        +LoginAsync()
        +GetAccountInfoAsync()
    }

    class IComponentCatalogService {
        <<interface>>
        +GetBrandsAsync()
        +GetLayoutsAsync()
        +GetAvailableKitsAsync()
        +GetAvailableSwitchesAsync()
        +GetAvailableKeycapSetsAsync()
        +GetAvailableStabilizersAsync()
        +GetAvailableAccessoriesAsync()
    }

    class IBuildService {
        <<interface>>
        +GetBuyerBuildsAsync()
        +GetBuildByIdAsync()
        +ValidateBuildAsync()
        +CalculateTotalAsync()
        +SaveBuildAsync()
        +ArchiveBuildAsync()
    }

    class IRequestService {
        <<interface>>
        +GetAvailableSellersAsync()
        +SendRequestAsync()
        +GetBuyerRequestsAsync()
        +GetSellerRequestsAsync()
        +UpdateStatusAsync()
    }

    class IAdminService {
        <<interface>>
        +GetSummaryAsync()
        +GetUsersAsync()
        +SetUserActiveAsync()
        +SetUserRoleAsync()
        +GetSellerProfilesAsync()
        +SaveSellerProfileAsync()
        +SetSellerVerifiedAsync()
        +SaveBrandAsync()
        +SaveLayoutAsync()
        +SaveKitAsync()
        +SaveSwitchAsync()
        +SaveKeycapSetAsync()
        +SaveStabilizerAsync()
        +SaveAccessoryAsync()
        +SetCatalogItemAvailabilityAsync()
        +GetAuditLogsAsync()
    }

    class IChatService {
        <<interface>>
        +GetOrCreateConversationAsync()
        +GetMessagesAsync()
        +SendMessageAsync()
    }

    class IComponentRepository {
        <<interface>>
        +GetBrandsAsync()
        +GetLayoutsAsync()
        +GetKeyboardKitsAsync()
        +GetSwitchesAsync()
        +GetKeycapSetsAsync()
        +GetStabilizersAsync()
        +GetAccessoriesAsync()
        +SaveBrandAsync()
        +SaveLayoutAsync()
        +SaveKitAsync()
        +SaveSwitchAsync()
        +SaveKeycapSetAsync()
        +SaveStabilizerAsync()
        +SaveAccessoryAsync()
    }

    class IBuildRepository {
        <<interface>>
        +GetByBuyerAsync()
        +GetByIdAsync()
        +SaveAsync()
        +ArchiveAsync()
    }

    class IRequestRepository {
        <<interface>>
        +CreateAsync()
        +GetByBuyerAsync()
        +GetBySellerAsync()
        +GetByIdAsync()
        +UpdateStatusAsync()
    }

    class IUserRepository {
        <<interface>>
        +GetByIdAsync()
        +GetByEmailAsync()
        +SaveAsync()
        +SetActiveAsync()
        +SetRoleAsync()
    }

    class ISellerRepository {
        <<interface>>
        +GetVerifiedSellersAsync()
        +GetProfilesAsync()
        +SaveProfileAsync()
        +SetVerifiedAsync()
    }

    class IAuditLogRepository {
        <<interface>>
        +AddAsync()
        +GetRecentAsync()
    }

    class IChatRepository {
        <<interface>>
        +GetOrCreateConversationAsync()
        +GetMessagesAsync()
        +AddMessageAsync()
    }

    class BuildValidationResult {
        +bool IsValid
        +List~string~ Errors
        +List~string~ Warnings
        +decimal TotalCost
        +int RequiredSwitchQuantity
    }

    IComponentCatalogService --> IComponentRepository
    IBuildService --> IBuildRepository
    IBuildService --> IComponentCatalogService
    IBuildService --> BuildValidationResult
    IRequestService --> IRequestRepository
    IRequestService --> IBuildRepository
    IRequestService --> ISellerRepository
    IRequestService --> IUserRepository
    IAdminService --> IUserRepository
    IAdminService --> ISellerRepository
    IAdminService --> IComponentRepository
    IAdminService --> IAuditLogRepository
    IChatService --> IChatRepository
    IChatService --> IUserRepository
    IChatService --> IRequestRepository
```

## MVVM/ViewModel Class Diagram

```mermaid
classDiagram
    direction LR

    class MainShellViewModel {
        +ViewModelBase CurrentViewModel
        +User? CurrentUser
        +NavigateToLogin()
        +NavigateByRole()
        +Logout()
    }

    class LoginViewModel {
        +string Email
        +LoginCommand
    }

    class RegisterViewModel {
        +string Username
        +string Email
        +RegisterCommand
    }

    class BuyerDashboardViewModel {
        +ObservableCollection~KeyboardBuild~ Builds
        +ObservableCollection~BuildRequest~ Requests
        +StartNewBuildCommand
        +SaveBuildCommand
        +ArchiveBuildCommand
        +SendRequestCommand
    }

    class BuildEditorViewModel {
        +KeyboardBuild CurrentBuild
        +KeyboardKit? SelectedKit
        +ObservableCollection~BuildItem~ Items
        +AddSwitchCommand
        +AddKeycapCommand
        +AddStabilizerCommand
        +AddAccessoryCommand
        +AddModCommand
        +ValidateCommand
    }

    class SellerDashboardViewModel {
        +ObservableCollection~BuildRequest~ Requests
        +OpenRequestCommand
        +AcceptCommand
        +MarkInProgressCommand
        +CompleteCommand
        +CancelCommand
    }

    class AdminDashboardViewModel {
        +ObservableCollection~User~ Users
        +ObservableCollection~SellerProfile~ Sellers
        +ObservableCollection~KeyboardKit~ Kits
        +ObservableCollection~KeyboardSwitch~ Switches
        +ObservableCollection~KeycapSet~ Keycaps
        +ObservableCollection~Stabilizer~ Stabilizers
        +ObservableCollection~Accessory~ Accessories
        +SaveCatalogCommand
        +SetAvailabilityCommand
        +VerifySellerCommand
    }

    class ChatViewModel {
        +ChatConversation CurrentConversation
        +ObservableCollection~ChatMessage~ Messages
        +SendMessageCommand
    }

    MainShellViewModel --> LoginViewModel
    MainShellViewModel --> RegisterViewModel
    MainShellViewModel --> BuyerDashboardViewModel
    MainShellViewModel --> SellerDashboardViewModel
    MainShellViewModel --> AdminDashboardViewModel
    BuyerDashboardViewModel --> BuildEditorViewModel
    BuyerDashboardViewModel --> ChatViewModel
    SellerDashboardViewModel --> ChatViewModel
    AdminDashboardViewModel --> ChatViewModel
```

## Logic Validation Can Nam Trong BuildService

| Nhom | Kiem tra |
| --- | --- |
| Build metadata | Buyer active, ten build khong rong, status hop le. |
| Kit | Kit ton tai, available, co layout hop le, `RequiredSwitchQuantity > 0`. |
| BuildItem | Moi item set dung mot FK san pham; `Quantity > 0`; item ton tai va available. |
| Switch | Switch technology khop kit `PcbTechnology`; switch mount khop kit `SwitchMount`; tong quantity switch >= `RequiredSwitchQuantity`. |
| Keycap | `SupportedFormFactor` phu hop layout/form factor cua kit. |
| Stabilizer | `SupportedLayouts` phu hop layout/form factor cua kit. |
| Accessory | `TargetComponent` nam trong Switch/Stabilizer/Kit/General. |
| Price | `TotalCostSnapshot = kit price + sum(item quantity * unit price snapshot)`. |

## Mapping Voi FHD Refactor

| FHD | Lop chinh |
| --- | --- |
| 1. Tai khoan | `User`, `Role`, `IAccountService`, `LoginViewModel`, `RegisterViewModel` |
| 2. Buyer - Tao build tu kit | `KeyboardBuild`, `KeyboardKit`, `BuildItem`, `BuildMod`, `IBuildService`, `BuyerDashboardViewModel`, `BuildEditorViewModel` |
| 3. Seller - Xu ly request | `BuildRequest`, `IRequestService`, `SellerDashboardViewModel` |
| 4. Admin - Quan tri he thong | `User`, `SellerProfile`, catalog classes, `IAdminService`, `AdminDashboardViewModel`, `AuditLogEntry` |
| 5. Chat | `ChatConversation`, `ChatMessage`, `IChatService`, `ChatViewModel` |

## Cac Lop Cu Can Loai Bo Hoac Thay The Khi Refactor

| Lop/interface hien tai | Huong refactor |
| --- | --- |
| `KeyboardCase` | Bo khoi build flow; thong tin case nam trong `KeyboardKit.IncludedParts`. |
| `Pcb` | Bo khoi build flow; `KeyboardKit.PcbTechnology` va `KeyboardKit.SwitchMount` la du cho validation. |
| `Plate` | Bo khoi build flow; nam trong `KeyboardKit.IncludedParts`. |
| `CompatibilityRule` | Bo; thay bang validation truc tiep giua kit va build items. |
| `KeyboardBuild.SwitchId/KeycapId/StabilizerId` | Bo; thay bang `KeyboardBuild.Items`. |
| `KeyboardBuild.LayoutId` | Bo; layout lay qua `KeyboardKit.LayoutId`. |
| `KeyboardBuild.SwitchQuantity` | Khong tao; quantity nam tren `BuildItem`. |
| `IComponentCatalogService.GetCasesForLayoutAsync` | Bo. |
| `IComponentCatalogService.GetPcbsForLayoutAsync` | Bo. |
| `IComponentCatalogService.GetPlatesForLayoutAsync` | Bo. |
| `IComponentCatalogService.GetCompatibilityRulesAsync` | Bo. |
| `AdminComponentType.Case/Pcb/Plate` | Bo; them `Kit`, `Switch`, `KeycapSet`, `Stabilizer`, `Accessory`. |

## Ghi Chu Trien Khai

- File nay la target class diagram cho refactor, khong phai anh chup code hien tai.
- Nen refactor theo thu tu: domain models -> repository interfaces -> services -> view models -> SQL schema/seed.
- `BuildItem` dung nullable FK theo ERD, nhung trong code nen co helper `HasExactlyOneProduct()` de validation ro rang.
- `BuildRequest` khong luu `BuyerId`; buyer lay qua `Build.BuyerId`.
- Chat conversation luon co seller va dung mot trong `BuyerId` hoac `AdminUserId`.

