# Ke hoach refactor PK ve cot `id`

## 1. Muc tieu

Chuan hoa ten khoa chinh (Primary Key - PK) cua tat ca bang trong database ve `id` thay vi moi bang dung mot ten rieng nhu `user_id`, `build_id`, `kit_id`.

Muc tieu cuoi:

- Trong moi bang, PK co ten thong nhat la `id`.
- Cac khoa ngoai (Foreign Key - FK) o bang con van giu ten co nghia nhu `user_id`, `build_id`, `seller_user_id`, `kit_id`.
- Code C# van co the giu property co nghia nhu `UserId`, `BuildId`, `KitId` bang cach query SQL alias `id AS user_id` neu can.
- ERD, SQL schema, seed data, verify script va repository SQL phai dong bo voi nhau.

## 2. Nguyen tac quan trong

Khong duoc replace toan cuc tat ca `*_id` thanh `id`.

Ly do: trong bang con, cac cot nhu `user_id`, `build_id`, `kit_id`, `seller_user_id`, `conversation_id` la FK va can giu ten co nghia de biet dang tro den bang nao.

Vi du dung:

```sql
CREATE TABLE users (
    id INT IDENTITY(1,1) PRIMARY KEY,
    role_id INT NOT NULL,
    CONSTRAINT FK_users_roles FOREIGN KEY (role_id) REFERENCES roles(id)
);
```

Vi du khong nen lam:

```sql
CREATE TABLE users (
    id INT IDENTITY(1,1) PRIMARY KEY,
    id INT NOT NULL
);
```

## 3. Danh sach PK can doi

| Bang | PK hien tai | PK sau refactor | Kieu du lieu |
|---|---|---|---|
| `roles` | `role_id` | `id` | `INT IDENTITY` |
| `users` | `user_id` | `id` | `INT IDENTITY` |
| `seller_profiles` | `seller_profile_id` | `id` | `INT IDENTITY` |
| `seller_applications` | `application_id` | `id` | `INT IDENTITY` |
| `brands` | `brand_id` | `id` | `INT IDENTITY` |
| `layouts` | `layout_id` | `id` | `VARCHAR(50)` |
| `keyboard_kits` | `kit_id` | `id` | `VARCHAR(50)` |
| `switches` | `switch_id` | `id` | `VARCHAR(50)` |
| `keycap_sets` | `keycap_id` | `id` | `VARCHAR(50)` |
| `stabilizers` | `stab_id` | `id` | `VARCHAR(50)` |
| `accessories` | `accessory_id` | `id` | `VARCHAR(50)` |
| `builds` | `build_id` | `id` | `VARCHAR(50)` |
| `build_items` | `build_item_id` | `id` | `INT IDENTITY` |
| `build_mods` | `mod_id` | `id` | `INT IDENTITY` |
| `build_requests` | `request_id` | `id` | `VARCHAR(50)` |
| `devices` | `device_id` | `id` | `VARCHAR(50)` |
| `device_test_sessions` | `session_id` | `id` | `VARCHAR(50)` |
| `device_key_test_results` | `key_test_id` | `id` | `BIGINT IDENTITY` |
| `audit_log` | `log_id` | `id` | `INT IDENTITY` |
| `chat_conversations` | `conversation_id` | `id` | `VARCHAR(50)` |
| `chat_messages` | `message_id` | `id` | `VARCHAR(50)` |

## 4. FK nen giu nguyen ten, chi doi cot duoc reference

| Cot FK | Dang reference hien tai | Sau refactor |
|---|---|---|
| `users.role_id` | `roles(role_id)` | `roles(id)` |
| `seller_profiles.user_id` | `users(user_id)` | `users(id)` |
| `seller_applications.buyer_user_id` | `users(user_id)` | `users(id)` |
| `seller_applications.reviewed_by` | `users(user_id)` | `users(id)` |
| `keyboard_kits.brand_id` | `brands(brand_id)` | `brands(id)` |
| `keyboard_kits.layout_id` | `layouts(layout_id)` | `layouts(id)` |
| `switches.brand_id` | `brands(brand_id)` | `brands(id)` |
| `keycap_sets.brand_id` | `brands(brand_id)` | `brands(id)` |
| `stabilizers.brand_id` | `brands(brand_id)` | `brands(id)` |
| `builds.buyer_id` | `users(user_id)` | `users(id)` |
| `builds.kit_id` | `keyboard_kits(kit_id)` | `keyboard_kits(id)` |
| `build_items.build_id` | `builds(build_id)` | `builds(id)` |
| `build_items.switch_id` | `switches(switch_id)` | `switches(id)` |
| `build_items.keycap_id` | `keycap_sets(keycap_id)` | `keycap_sets(id)` |
| `build_items.stab_id` | `stabilizers(stab_id)` | `stabilizers(id)` |
| `build_items.accessory_id` | `accessories(accessory_id)` | `accessories(id)` |
| `build_mods.build_id` | `builds(build_id)` | `builds(id)` |
| `build_requests.build_id` | `builds(build_id)` | `builds(id)` |
| `build_requests.seller_user_id` | `users(user_id)` | `users(id)` |
| `devices.seller_user_id` | `users(user_id)` | `users(id)` |
| `device_test_sessions.request_id` | `build_requests(request_id)` | `build_requests(id)` |
| `device_test_sessions.device_id` | `devices(device_id)` | `devices(id)` |
| `device_test_sessions.seller_user_id` | `users(user_id)` | `users(id)` |
| `device_key_test_results.session_id` | `device_test_sessions(session_id)` | `device_test_sessions(id)` |
| `device_key_test_results.request_id` | `build_requests(request_id)` | `build_requests(id)` |
| `device_key_test_results.device_id` | `devices(device_id)` | `devices(id)` |
| `audit_log.user_id` | `users(user_id)` | `users(id)` |
| `chat_conversations.seller_user_id` | `users(user_id)` | `users(id)` |
| `chat_conversations.buyer_id` | `users(user_id)` | `users(id)` |
| `chat_conversations.admin_user_id` | `users(user_id)` | `users(id)` |
| `chat_conversations.build_request_id` | `build_requests(request_id)` | `build_requests(id)` |
| `chat_messages.conversation_id` | `chat_conversations(conversation_id)` | `chat_conversations(id)` |
| `chat_messages.sender_user_id` | `users(user_id)` | `users(id)` |

## 5. File can chinh sua

### 5.1. Database schema va scripts

| File | Viec can lam |
|---|---|
| `Database/SqlServer/CreateSchema_Refactor.sql` | Doi PK tung bang thanh `id`; cap nhat `REFERENCES`; cap nhat index/order/query noi dung script. |
| `Documents_Refactor/SeedData_Refactor.sql` | Cap nhat `MERGE`, `JOIN`, `INSERT`, `ON target.old_pk`, `source.old_pk` theo PK moi. |
| `Database/SqlServer/SeedDemoAnalytics_Refactor.sql` | Cap nhat demo seed query, insert, delete, join theo PK moi. |
| `Database/SqlServer/VerifyRefactor.sql` | Cap nhat tat ca verification query, orphan check, duplicate check, join check. |
| `Database/SqlServer/ApplySellerApplications.sql` | Doi `application_id` PK thanh `id`, update FK reference den `users(id)`. |

### 5.2. ERD va tai lieu

| File | Viec can lam |
|---|---|
| `Documents_Refactor/Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml` | Doi moi PK `[pk]` thanh `id`; update cac `ref: > table.old_pk`. |
| `Documents_Refactor/CUSTOM_KEYBOARD_PROJECT_OVERVIEW.md` | Cap nhat bang mo ta schema. |
| `Documents_Refactor/SeedData_Refactor_Check.md` | Cap nhat cac dong noi den `builds.kit_id` neu can giai thich ro FK. |
| `README.md` | Cap nhat cac note ve ERD/schema neu co noi den PK cu. |

### 5.3. Repository SQL trong C#

Tat ca file duoi day dang co SQL string tham chieu ten PK/FK cu va can duoc sua co chon loc:

| File | Vung anh huong chinh |
|---|---|
| `Repositories/SqlServer/SqlUserRepository.cs` | `users.user_id`, `roles.role_id`, mapping `UserId`, `RoleId`. |
| `Repositories/SqlServer/SqlSellerRepository.cs` | `seller_profiles.seller_profile_id`, `users.user_id`, `roles.role_id`. |
| `Repositories/SqlServer/SqlSellerApplicationRepository.cs` | `seller_applications.application_id`, `users.user_id`, `roles.role_id`, audit insert. |
| `Repositories/SqlServer/SqlComponentRepository.cs` | `brand_id`, `layout_id`, `kit_id`, `switch_id`, `keycap_id`, `stab_id`, `accessory_id`. |
| `Repositories/SqlServer/SqlBuildRepository.cs` | `build_id`, `build_item_id`, `mod_id`, product FK queries. |
| `Repositories/SqlServer/SqlRequestRepository.cs` | `request_id`, `build_id`, seller/buyer joins. |
| `Repositories/SqlServer/SqlStatsRepository.cs` | Analytics joins over `build_requests`, `builds`, `keyboard_kits`, `users`, `seller_profiles`. |
| `Repositories/SqlServer/SqlChatRepository.cs` | `conversation_id`, `message_id`, user FK joins. |
| `Repositories/SqlServer/SqlAuditLogRepository.cs` | `log_id`, `user_id`. |
| `Repositories/SqlServer/SqlDeviceRepository.cs` | `device_id`, `seller_user_id`. |
| `Repositories/SqlServer/SqlDeviceTestSessionRepository.cs` | `session_id`, `request_id`, `device_id`. |
| `Repositories/SqlServer/SqlDeviceKeyTestResultRepository.cs` | `key_test_id`, `session_id`, `request_id`, `device_id`. |

## 6. Khuyen nghi giu model C# nhu hien tai

Khong bat buoc doi property C# tu `UserId`, `BuildId`, `KitId` thanh `Id`.

Ly do:

- Trong code C#, `UserId`, `BuildId`, `KitId` de doc hon `Id` khi truyen qua service/viewmodel.
- Doi model se lam lan sang UI/ViewModel/Service nhieu hon can thiet.
- Co the dung alias SQL de giu mapping cu.

Vi du:

```sql
SELECT
    u.id AS user_id,
    u.role_id,
    r.role_name
FROM users AS u
INNER JOIN roles AS r ON r.id = u.role_id;
```

Repository van doc:

```csharp
UserId = reader.GetIntValue("user_id")
```

## 7. Thu tu thuc hien de giam rui ro

1. Tao branch rieng truoc khi sua.
2. Sua DBML/ERD truoc de co source of truth moi.
3. Sua `CreateSchema_Refactor.sql`.
4. Sua seed scripts.
5. Sua verify scripts.
6. Sua repositories theo tung domain:
   - Account/role/seller.
   - Component catalog.
   - Build/request.
   - Chat/audit.
   - Device/QC.
   - Stats/admin dashboard.
7. Chay build C#.
8. Tao lai database clean bang schema moi.
9. Chay seed.
10. Chay verify SQL.
11. Chay app smoke test theo role Buyer, Seller, Admin.

## 8. Checklist sua SQL an toan

Khi sua tung query:

- `SELECT old_pk` nen thanh `SELECT alias.id AS old_pk` neu C# mapping van doc `old_pk`.
- `JOIN parent p ON p.old_pk = child.fk` thanh `JOIN parent p ON p.id = child.fk`.
- `WHERE old_pk = @old_pk` tren bang chinh thanh `WHERE id = @old_pk`.
- `ORDER BY old_pk` tren bang chinh thanh `ORDER BY id`.
- `INSERT INTO table (old_pk, ...)` thanh `INSERT INTO table (id, ...)` voi cac PK dang la varchar/string.
- Bang co `IDENTITY` khong insert `id` truc tiep, tru truong hop co ly do dac biet.
- Cac bien/parameter nhu `@user_id`, `@build_id`, `@request_id` co the giu nguyen vi day la domain meaning trong code.

## 9. Rui ro can chu y

- `id` la ten cot lap lai o tat ca bang, query join phai luon dung alias ro rang: `u.id`, `b.id`, `br.id`.
- `SELECT *` se nguy hiem hon khi join nhieu bang vi nhieu cot cung ten `id`; nen tranh `SELECT *`.
- Cac query dynamic trong `SqlComponentRepository.cs` can sua can than vi hien tai co mapping component type sang ten cot PK.
- Cac unique index/foreign key/index name khong bat buoc doi, nhung nen cap nhat neu ten qua lech ngu nghia.
- Seed data dung `MERGE` rat nhay voi `ON target.old_pk`; can sua tung bang mot.
- Database hien tai can recreate clean neu day la test DB. Migration in-place doi ten cot tren DB dang co du lieu se phuc tap hon.

## 10. Lenh kiem tra sau khi sua

Build project chinh:

```powershell
dotnet build Custom_keyboard.csproj
```

Kiem tra con sot PK cu trong SQL/schema/doc/code:

```powershell
rg -n "\b(role_id|user_id|seller_profile_id|application_id|brand_id|layout_id|kit_id|switch_id|keycap_id|stab_id|accessory_id|build_id|build_item_id|mod_id|request_id|device_id|session_id|key_test_id|log_id|conversation_id|message_id)\b" Database Documents_Refactor Repositories Services Models README.md
```

Luu y: lenh tren van se con ket qua hop le cho cac FK nhu `user_id`, `build_id`, `kit_id`. Khi review can phan biet:

- Neu nam o bang chinh va la PK cu: can doi.
- Neu nam o bang con va la FK co nghia: co the giu.

## 11. Trang thai baseline truoc refactor

Tai thoi diem lap ke hoach:

- `dotnet build Custom_keyboard.csproj` thanh cong.
- `dotnet build` solution hien fail vi `.slnx` dang tham chieu `Phase6Verification/Phase6Verification.csproj` va `WpfUiVerification/WpfUiVerification.csproj`, trong khi hai project nay dang bi xoa trong worktree.
- Worktree da co nhieu file modified/deleted tu truoc; khi thuc hien refactor can tranh revert nham cac thay doi co san.

