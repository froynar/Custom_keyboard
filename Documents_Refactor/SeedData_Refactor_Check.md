# Seed Data Refactor - Cross Check

## Target

- ERD: `Documents_Refactor/Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml`
- Dataset: `Documents_Refactor/SeedData_Refactor.sql`
- Scope: seed data for the refactored ERD only, not the current runtime schema.

## Table Coverage

| ERD table | Seed coverage | Purpose |
| --- | ---: | --- |
| roles | 3 rows | Buyer, Seller, Admin permissions |
| users | 7 rows | Active/inactive buyers, verified/unverified sellers, admin |
| seller_profiles | 3 rows | Seller verification scenarios |
| brands | 13 rows | Brands used by kits, switches, keycaps, stabs |
| layouts | 4 rows | 65, 75, TKL, 100 |
| keyboard_kits | 9 rows | Mechanical, HE, available/unavailable kits |
| switches | 10 rows | MX 3-pin, MX 5-pin, HE, available/unavailable switches |
| keycap_sets | 5 rows | Universal and layout-limited keycap sets |
| stabilizers | 4 rows | Simplified stabilizer packages per layout group |
| accessories | 7 rows | Lube, film, cable, foam, tool add-ons |
| builds | 5 rows | Draft, Saved, Requested, Archived build states |
| build_items | 17 rows | Switch, keycap, stab, accessory choices |
| build_mods | 5 rows | Lube, tune, calibration, film scenarios |
| build_requests | 2 rows | Pending and Completed seller requests |
| audit_log | 3 rows | Admin/catalog/status audit examples |
| chat_conversations | 2 rows | Buyer-seller and admin-seller conversations |
| chat_messages | 4 rows | Messages tied to valid conversations and senders |

Result: every ERD table has seed coverage. No extra old table is used.

## Business Logic Checks

| Check | Result |
| --- | --- |
| No `seller_inventory`, `inventory`, `stock`, `seller_price` seed logic | Pass |
| No old separate `case_id`, `pcb_id`, `plate_id` component choices | Pass |
| No old switch-mod columns such as `lube_type`, `is_filmed`, `spring_weight_g` | Pass |
| Each `build_items` row sets exactly one product FK | Pass |
| Kit is stored only once on `builds.kit_id` | Pass |
| Switch quantity matches `keyboard_kits.required_switch_quantity` for complete builds | Pass |
| Draft build can be intentionally incomplete for UI warning tests | Pass |
| Requested builds have matching `build_requests` rows | Pass |
| Seller request targets active, verified seller users | Pass |
| Buyer-seller chat may link to a request; admin-seller chat does not require a request | Pass |
| Chat conversation has exactly one of `buyer_id` or `admin_user_id` | Pass |

## Build Total Checks

| Build | Calculation | Snapshot | Result |
| --- | ---: | ---: | --- |
| BUILD_REF_NEO65_MECH | 140.00 + 70 * 0.34 + 35.00 + 18.00 + 8.50 = 225.30 | 225.30 | Pass |
| BUILD_REF_BOOG75_HE | 220.00 + 85 * 1.00 + 45.00 + 16.00 + 18.00 = 384.00 | 384.00 | Pass |
| BUILD_REF_QK65_THOCK | 274.40 + 70 * 0.66 + 25.00 + 18.00 + 5.00 = 368.60 | 368.60 | Pass |
| BUILD_REF_AULA_DRAFT | 40.00 + 85 * 0.58 = 89.30 | 89.30 | Pass |
| BUILD_REF_ARCHIVED_NEO80 | 250.00 + 90 * 0.46 + 35.00 + 16.00 + 7.00 = 349.40 | 349.40 | Pass |

## Compatibility Scenarios Covered

| Scenario | Dataset rows |
| --- | --- |
| Mechanical MX 5-pin kit with MX 5-pin switch | `KIT_NEO65` + `SW_NEO_AZURE` |
| Mechanical MX 3-pin kit with MX 3-pin switch | `KIT_QK65` + `SW_GATERON_OIL_KING` |
| HE kit with HE switch | `KIT_BOOG75_HE` + `SW_GATERON_MAGNETIC_JADE_PRO` |
| Incomplete draft build | `BUILD_REF_AULA_DRAFT` |
| Unavailable catalog item retained for archive/filter tests | `KIT_TOFU65_ARCHIVE`, `SW_BSUN_HUTT`, archive keycap/stab/cable |

## Notes For Refactor

- This seed intentionally keeps compatibility simple, matching the ERD simplification: keycap/stabilizer compatibility is represented by layout/form-factor text, not detailed physical standards.
- The seed does not model seller stock. Seller selection is request-based through `build_requests.seller_user_id`.
- `request_payload_json` snapshots the chosen build items so seller workflow can remain stable even if catalog price or availability changes later.
