# Custom Keyboard Builder - Current Architecture

This document describes the current refactored WPF application. The active source of truth is the kit-based ERD and supporting documents in `Documents_Refactor/`. The older `Documents/` folder is kept only as historical reference.

## Current Status

- Refactor model: kit-based build flow with `keyboard_kits` + `build_items`.
- Runtime: WPF on .NET, MVVM, SQL Server database `CustomKeyboard_Refactor`.
- Phase 6 verification: implemented in `Phase6Verification/`.
- Baseline commands:

```powershell
dotnet build
dotnet run --project Phase6Verification\Phase6Verification.csproj
```

## Layers

| Folder | Purpose |
| --- | --- |
| `Models` | Domain/entity classes for accounts, kit-based catalog, builds, requests, chat, and admin audit data. |
| `ViewModels` | MVVM state and commands for login/register, buyer, seller, admin, build configuration, and chat. |
| `Views` | WPF UserControls/Windows for the role dashboards and shared chat view. |
| `Services` | Business workflows: account, catalog, build validation/price snapshot, request state machine, chat, and admin operations. |
| `Repositories` | Data access contracts plus SQL Server implementations. |
| `Data/SqlServer` | SQL Server settings, connection factory, and health check helpers. |
| `Database/SqlServer` | Refactor schema, verification SQL, and legacy scripts. |
| `Phase6Verification` | Self-contained verification runner for unit-style service checks, SQL integration checks, and DB invariants. |

## Runtime Composition

`MainWindow.xaml.cs` wires the application manually:

1. `SqlConnectionFactory`
2. SQL repositories
3. services
4. `MainShellViewModel`
5. role-specific dashboard ViewModels after login

No DI container is required for the current scope. Keeping composition explicit makes the refactor easier to inspect for coursework/demo purposes.

## Main Flows

### Account

- `AccountService` authenticates username/email + PBKDF2 password hash.
- Inactive users are blocked.
- `MainShellViewModel` routes Buyer/Seller/Admin users to their dashboards.

### Buyer

- Buyer chooses one `KeyboardKit`.
- Buyer adds `BuildItem` rows for switch, keycap, stabilizer, and accessories.
- `BuildService` validates compatibility and computes `TotalCostSnapshot`.
- Buyer saves a build, sends a request to a verified seller, tracks request status, and can chat with the seller.

### Seller

- Seller sees only requests assigned to their user id.
- `RequestService` enforces status transitions:
  `Pending -> Accepted -> In_progress -> Completed`, with `Cancelled` allowed from active states.
- Seller can read the immutable `request_payload_json` snapshot.

### Admin

- Admin manages users, seller profiles, brand/layout/catalog records, and audit log.
- Catalog management covers kit, switch, keycap, stabilizer, and accessory.

### Chat

- `ChatService` allows buyer-seller and admin-seller conversations.
- Buyer-admin direct chat is intentionally out of scope.
- Read/send operations validate that the requesting user belongs to the conversation.

## Database Model

The refactor targets 17 tables:

- Account/admin: `roles`, `users`, `seller_profiles`, `audit_log`
- Catalog: `brands`, `layouts`, `keyboard_kits`, `switches`, `keycap_sets`, `stabilizers`, `accessories`
- Build/request: `builds`, `build_items`, `build_mods`, `build_requests`
- Chat: `chat_conversations`, `chat_messages`

Removed from the active design:

- separate `cases`, `pcbs`, `plates`
- layout junction tables for case/PCB/plate
- `compatibility_rules`
- `seller_inventory`
- detailed switch-mod columns such as `lube_type`, `is_filmed`, `spring_weight_g`

## Verification Strategy

Phase 6 uses two verification layers:

1. `dotnet build` confirms compile safety for the WPF app and verification runner.
2. `Phase6Verification` checks service behavior, writes temporary SQL rows with a `P6_` prefix, cleans them up, and verifies DB invariants.

The live DB may contain additional registered users and app-created builds/requests beyond the seed baseline. The verifier therefore requires exact counts for static seed/catalog tables and minimum seed counts for users plus mutable transaction tables while still requiring invariant queries to return zero errors.
