# Custom Keyboard Builder - Class Diagram Report Style

Tai lieu nay tao Class Diagram theo phong cach bao cao UML gan voi domain/data model. Khac voi file `Custom_Keyboard_Class_Diagram.md` dang thien ve lop nghiep vu/service, file nay tap trung vao:

- Cac lop thuc the chinh trong he thong.
- Thuoc tinh va kieu du lieu.
- Quan he va boi so giua cac lop.
- Cach trinh bay gan voi mau class diagram trong bao cao.

## Class Diagram

```mermaid
classDiagram
    direction LR

    class Role {
        +role_id: INT
        +role_name: VARCHAR
        +permissions: VARCHAR
    }

    class User {
        +user_id: INT
        +username: VARCHAR
        +email: VARCHAR
        +phone: VARCHAR
        +password_hash: VARCHAR
        +is_active: BIT
    }

    class SellerProfile {
        +seller_profile_id: INT
        +shop_name: VARCHAR
        +phone: VARCHAR
        +address: VARCHAR
        +is_verified: BIT
        +assigned_at: DATETIME
        +verified_at: DATETIME
    }

    class AuditLog {
        +log_id: INT
        +table_name: VARCHAR
        +record_id: VARCHAR
        +action: VARCHAR
        +old_value_json: TEXT
        +new_value_json: TEXT
        +changed_at: DATETIME
    }

    class Brand {
        +brand_id: INT
        +brand_name: VARCHAR
        +country: VARCHAR
    }

    class Layout {
        +layout_id: VARCHAR
        +layout_name: VARCHAR
        +form_factor: VARCHAR
        +standard_key_count: INT
    }

    class KeyboardCase {
        +case_id: VARCHAR
        +material: VARCHAR
        +mount_type: VARCHAR
        +color: VARCHAR
        +weight_g: INT
        +price_usd: FLOAT
        +is_available: BIT
    }

    class PCB {
        +pcb_id: VARCHAR
        +pcb_technology: VARCHAR
        +mount_type: VARCHAR
        +hotswap: BIT
        +wireless: BIT
        +rgb: BIT
        +switch_mount: VARCHAR
        +price_usd: FLOAT
        +is_available: BIT
    }

    class Plate {
        +plate_id: VARCHAR
        +material: VARCHAR
        +mount_type: VARCHAR
        +flex_cut: VARCHAR
        +price_usd: FLOAT
        +is_available: BIT
    }

    class Switch {
        +switch_id: VARCHAR
        +switch_technology: VARCHAR
        +switch_type: VARCHAR
        +actuation_force_g: INT
        +mount_type: VARCHAR
        +sound_profile: VARCHAR
        +price_usd: FLOAT
        +is_available: BIT
    }

    class KeycapSet {
        +keycap_id: VARCHAR
        +profile: VARCHAR
        +material: VARCHAR
        +color_primary: VARCHAR
        +legend_type: VARCHAR
        +price_usd: FLOAT
        +is_available: BIT
    }

    class Stabilizer {
        +stab_id: VARCHAR
        +stab_type: VARCHAR
        +sizes_included: VARCHAR
        +price_usd: FLOAT
        +is_available: BIT
    }

    class Build {
        +build_id: VARCHAR
        +name: VARCHAR
        +status: VARCHAR
        +total_cost_snapshot: FLOAT
        +created_at: DATETIME
        +updated_at: DATETIME
    }

    class BuildMod {
        +mod_id: INT
        +mod_type: VARCHAR
        +target_component: VARCHAR
        +lube_type: VARCHAR
        +is_filmed: BIT
        +spring_weight_g: INT
        +notes: VARCHAR
    }

    class BuildRequest {
        +request_id: VARCHAR
        +request_payload_json: TEXT
        +status: VARCHAR
        +note: VARCHAR
        +requested_at: DATETIME
        +accepted_at: DATETIME
        +completed_at: DATETIME
        +updated_at: DATETIME
    }

    class CompatibilityRule {
        +rule_id: INT
        +case_id: VARCHAR
        +pcb_id: VARCHAR
        +plate_id: VARCHAR
        +is_compatible: BIT
        +notes: VARCHAR
    }

    Role "1" --> "0..*" User : has
    User "1" --> "0..1" SellerProfile : owns
    User "1" --> "0..*" AuditLog : creates
    User "1" --> "0..*" Build : creates

    Brand "1" --> "0..*" KeyboardCase : brands
    Brand "1" --> "0..*" PCB : brands
    Brand "1" --> "0..*" Plate : brands
    Brand "1" --> "0..*" Switch : brands
    Brand "1" --> "0..*" KeycapSet : brands
    Brand "1" --> "0..*" Stabilizer : brands

    Layout "1" --> "0..*" Build : selected_in
    Layout "1..*" -- "0..*" KeyboardCase : supports
    Layout "1..*" -- "0..*" PCB : supports
    Layout "1..*" -- "0..*" Plate : supports

    Build "1" --> "1" User : buyer
    Build "1" --> "1" Layout : layout
    Build "1" --> "0..1" KeyboardCase : case
    Build "1" --> "0..1" PCB : pcb
    Build "1" --> "0..1" Plate : plate
    Build "1" --> "0..1" Switch : switch
    Build "1" --> "0..1" KeycapSet : keycap
    Build "1" --> "0..1" Stabilizer : stabilizer
    Build "1" --> "0..*" BuildMod : mods

    Build "1" --> "0..*" BuildRequest : requests
    BuildRequest "1" --> "1" User : buyer
    BuildRequest "1" --> "1" User : seller

    CompatibilityRule "0..*" --> "0..1" KeyboardCase : checks
    CompatibilityRule "0..*" --> "0..1" PCB : checks
    CompatibilityRule "0..*" --> "0..1" Plate : checks
```

## Giai Thich Quan He Chinh

| Quan he | Y nghia |
| --- | --- |
| `Role 1 - 0..* User` | Mot role co nhieu user; moi user thuoc mot role. |
| `User 1 - 0..1 SellerProfile` | User chi co seller profile khi duoc phan quyen seller. |
| `User 1 - 0..* Build` | Buyer co the tao nhieu build. |
| `Build 1 - 0..* BuildRequest` | Mot build co the duoc gui thanh request. |
| `BuildRequest 1 - 1 User buyer/seller` | Request luon co buyer gui va seller nhan. |
| `Build 1 - 0..1 Component` | Moi build co the gan tung loai linh kien: case, PCB, plate, switch, keycap, stabilizer. |
| `Layout - Component` | Layout quy dinh nhung case, PCB, plate nao co the ho tro. |
| `CompatibilityRule - Case/PCB/Plate` | Rule dung de xac dinh do tuong thich giua case, PCB va plate; technology/mount cua switch duoc kiem tra truc tiep bang thuoc tinh cua PCB va switch trong service. |
| `User 1 - 0..* AuditLog` | User, dac biet la admin, co the tao nhieu log thao tac. |

## Mapping Voi FHD

| Nhom FHD | Lop lien quan |
| --- | --- |
| 1. Tai khoan | `User`, `Role` |
| 2. Buyer - Tao va gui build | `Build`, `Layout`, `KeyboardCase`, `PCB`, `Plate`, `Switch`, `KeycapSet`, `Stabilizer`, `BuildMod`, `BuildRequest` |
| 3. Seller - Xu ly request | `BuildRequest`, `Build`, `User`, `SellerProfile` |
| 4. Admin - Quan tri | `User`, `Role`, `SellerProfile`, cac lop linh kien, `AuditLog` |

## Ghi Chu

- File nay co chu dich giong class diagram trong bao cao, nen thuoc tinh va kieu du lieu duoc ghi ro.
- Cac bang trung gian trong ERD nhu `case_layouts`, `pcb_layouts`, `plate_layouts` duoc bieu dien bang quan he `supports` giua `Layout` va linh kien.
- File `Custom_Keyboard_Class_Diagram.md` van duoc giu rieng de mo ta lop nghiep vu/service theo FHD.
