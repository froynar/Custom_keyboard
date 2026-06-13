# Phase 6 Verification Report

Date: 2026-06-12

## Scope

Phase 6 validates the kit-based refactor after the UI/service/repository work:

- service-level behavior for build validation/pricing, request status flow, and chat participant rules
- SQL integration with the `CustomKeyboard_Refactor` test database
- database invariants from `VerifyRefactor.sql`
- role UI smoke checks after login
- documentation refresh for current architecture and verification commands

## Commands

```powershell
dotnet build
dotnet run --project Phase6Verification\Phase6Verification.csproj
```

## Latest Result

```text
dotnet build: pass, 0 warnings, 0 errors
Phase6Verification: pass, 14/14 checks (updated 2026-06-13; Phase 6 baseline 10/10, +4 added in Phase 9/10 — login T01/T02, seller scoping T08/T09, admin-seller chat T14, seed-account login)
UI smoke: pass for buyer_refactor, seller_soigear, admin_refactor
```

## Automated Checks

`Phase6Verification` contains unit-style tests with fake repositories/services:

- `BuildService` calculates total = kit price + sum(item quantity * unit price).
- `BuildService` applies `UnitPriceSnapshot` and `TotalCostSnapshot`.
- `BuildService` rejects incompatible switch technology/mount combinations.
- `RequestService` creates snapshot payload JSON and enforces valid state transitions.
- `RequestService` rejects a build request sent to an unverified/inactive seller (verified seller still succeeds).
- `RequestService` publishes realtime after the DB write (correct seller/request/status) and a failing notifier never breaks the persisted result.
- `ChatService` blocks unverified sellers and non-participant reads/sends.
- `AccountService` rejects malformed email/phone on register and normalizes phone separators.
- `AdminService` writes an audit entry for ban, seller verify, and catalog (brand) changes, and blocks non-admin actors.
- `AccountService` logs in valid credentials, rejects a wrong password, and blocks a banned (inactive) user without creating a session (T01/T02, added Phase 9).
- `RequestService` scopes requests to the owning seller — another seller cannot see or update them (T08/T09, added Phase 9).
- `ChatService` supports admin-seller conversations persisted to and re-read from the DB (T14, added Phase 9).

It also contains SQL integration checks using real SQL repositories:

- create a temporary build with build items and mod notes
- reload the build and confirm item round-trip
- send a build request to a verified seller
- create a buyer-seller conversation and send one message
- cleanup temporary rows using the `P6_` data marker
- (separate check, added Phase 10) log in every active seed account with `Password123` and confirm the banned seed account (`buyer_inactive`) is blocked

## Database Invariants

The verification runner checks:

- static seed/catalog row counts match the expected baseline
- user and mutable transaction tables have at least the seed baseline rows
- users have required fields, unique username/email/phone, and active users use PBKDF2 hash format
- build total snapshots match the formula
- Saved/Requested builds have the required switch quantity
- catalog prices are non-negative across all five catalog tables
- kits require a positive switch quantity
- each `build_items` row has exactly one product FK
- build requests target active, verified sellers
- chat conversations have exactly one of buyer/admin
- no FK orphan rows exist across the refactor relationships
- Requested builds have a matching request
- chat message senders belong to their conversation

The live test DB currently contains one extra manually-created build named `kkkkkk`. User rows may also grow when registering buyers through the app, so users and mutable transaction tables are treated as seed minimums instead of exact live counts while account/data invariants still run.

## UI Smoke

The WPF app was launched and tested with:

- `buyer_refactor / Password123`
- `seller_soigear / Password123`
- `admin_refactor / Password123`

Each account reached the correct dashboard and no `Loi (UI thread)` popup appeared.

## Notes

- `Database/SqlServer/VerifyRefactor.sql` remains useful for seed-baseline inspection.
- For live DBs after manual app use, prefer `Phase6Verification` because it tolerates valid extra transaction rows while still enforcing invariants.
- The older `Documents/` folder remains historical reference only; `Documents_Refactor/` is the active source for the refactor model.
