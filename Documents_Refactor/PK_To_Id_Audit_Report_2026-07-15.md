# Báo cáo kiểm tra refactor khóa chính `*_id` → `id`

- **Ngày kiểm tra:** 2026-07-15
- **Phạm vi:** toàn bộ dự án liên quan trực tiếp đến việc đổi tên khóa chính (PK) thành `id`
- **Trạng thái bản audit gốc:** snapshot trước xử lý; nội dung từ mục 1 trở đi được giữ lại làm bằng chứng ban đầu
- **Cập nhật xử lý:** source đã được sửa và xác minh; runtime database chưa chạy migration và không bị ghi dữ liệu

## Cập nhật sau xử lý ngày 2026-07-15

| Finding | Trạng thái hiện tại |
| --- | --- |
| PKID-001 | Source/DBML/schema/repository đã thống nhất PK `id`; runtime vẫn là legacy `21/21` và đang bị startup guard chặn đúng thiết kế |
| PKID-002 | Đã có preflight, migration giữ dữ liệu bằng 21 `sp_rename`, postflight và schema version; chưa triển khai runtime |
| PKID-003 | Đã sửa toàn bộ owner-PK SQL, gồm `SqlSellerRepository`; alias `*_id` cho model vẫn được giữ |
| PKID-004 | Ứng dụng fail-fast theo version, chính xác 21 PK và 33 FK enabled/trusted |
| PKID-005 | `ApplySellerApplications.sql` được ghi rõ không phải bridge và từ chối schema legacy/mixed |
| PKID-006 | Đã tách verifier clean seed khỏi preflight/postflight migration, có orphan và `DBCC CHECKCONSTRAINTS` hard gate |
| PKID-007 | Seed có target/schema guard, transaction, JSON dùng ID resolve động và launcher không include DML trước khi postflight đạt |
| PKID-008 | Seller profile upsert theo unique `user_id`, kiểm tra cặp ID và chạy trong transaction `Serializable` |
| PKID-009 | `record_id` của `seller_profiles` dùng profile PK; migration preview chuẩn hóa `seller_soigear -> 2` và `3 -> 44` từ JSON lịch sử |
| PKID-010 | Clean seed phủ đủ 21 bảng, thêm QC fixture; source gate kiểm tra 21 PK, 33 FK và 21 rename |

Xác minh đã chạy:

- `VerifyPkToId_Source.ps1`: pass.
- `dotnet build Custom_keyboard.slnx`: pass, 0 warning, 0 error.
- Runtime preflight chỉ đọc: pass; row count và identity counter không đổi so với snapshot.
- Migration/postflight compile bằng `NOEXEC`: pass; schema/seed/verify parse bằng `PARSEONLY`: pass.
- Seed launcher trên runtime legacy dừng ở postflight version guard, không bind/chạy DML.
- `msdb.dbo.backupset` chưa có bản ghi backup cho `CustomKeyboard_Refactor` tại lần kiểm tra này.

**Điểm chặn còn lại:** bắt buộc tạo backup, restore thành clone, chạy đủ preflight → migration → postflight → smoke test trên clone. Chỉ sau khi rehearsal thành công mới được lặp lại trên runtime trong maintenance window. Không dùng `CreateSchema_Refactor.sql` cho database cần giữ dữ liệu.

## 1. Kết luận điều hành

Refactor hiện **chưa thể coi là hoàn tất**. Source code, DBML và script schema mới phần lớn đã chuyển sang PK `id`, nhưng database mà ứng dụng đang kết nối vẫn dùng tên PK cũ trên toàn bộ 21 bảng nghiệp vụ.

| Hạng mục                                           | Kết quả kiểm tra |
| -------------------------------------------------- | ---------------: |
| Bảng nghiệp vụ trong database runtime              |               21 |
| PK runtime đã có tên `id`                          |         **0/21** |
| PK runtime vẫn mang tên cũ                         |        **21/21** |
| FK trong database runtime                          |               33 |
| FK đang tham chiếu PK tên `id`                     |         **0/33** |
| Snapshot dòng dữ liệu ngày 2026-07-15              |        **3.658** |
| Script migration giữ dữ liệu                       |     **Không có** |
| SQL còn sót tên PK cũ, chắc chắn lỗi sau migration |     **1 vị trí** |

Ứng dụng cấu hình trực tiếp tới `CustomKeyboard_Refactor` tại `Data/SqlServer/SqlServerSettings.cs:5-6`. Các repository hiện đã đọc `table.id`, vì vậy database hiện tại trả lỗi `Invalid column name 'id'`. Lỗi xuất hiện từ luồng đăng nhập và lan tới hầu hết chức năng Buyer, Seller, Admin, chat, thống kê và QC.

**Khuyến nghị chính:** không đổi code quay lại tên PK cũ và không chạy `CreateSchema_Refactor.sql` trên database hiện tại. Cần tạo một migration bảo toàn dữ liệu, diễn tập trên bản restore, đổi đồng bộ cả 21 PK trong một đợt, đồng thời sửa câu SQL còn sót trong `SqlSellerRepository` trước khi phát hành.

## 2. Cách kiểm tra và giới hạn

Đã thực hiện:

- Đối chiếu DBML, schema SQL, seed, verify và các script bổ trợ.
- Rà toàn bộ repository SQL Server, service, model, ViewModel và composition root có liên quan tới ID.
- Đọc metadata trực tiếp của `KHOADZS1VN\SQLEXPRESS / CustomKeyboard_Refactor`.
- Kiểm tra PK, FK, kiểu dữ liệu, identity counter, row count, constraint, index và SQL module.
- Chạy truy vấn kiểm tra chỉ đọc; `VerifyRefactor.sql` được thử trên database hiện tại và dừng ở lỗi tên cột `id`.
- Đối chiếu ảnh hưởng theo từng luồng người dùng.

Không thực hiện:

- Không đổi tên cột, không chạy `sp_rename`, không chạy seed và không ghi database.
- Không sửa repository hay các file dự án khác.
- Không coi `dotnet build` là bằng chứng SQL đúng; raw SQL trong chuỗi C# không được compiler kiểm tra tên cột.

## 3. Trạng thái thực tế của 21 PK

DBML mới khai báo đủ 21 PK là `id` tại `Documents_Refactor/Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml:76-321`. Script clean schema cũng khai báo đủ 21 PK mới tại `Database/SqlServer/CreateSchema_Refactor.sql:54-434`.

Tại thời điểm audit ngày 2026-07-15, database runtime vẫn ở trạng thái cũ hoàn toàn, không phải migration dở dang một phần. Các row count/counter dưới đây là snapshot kiểm tra, không phải baseline cố định cho ngày triển khai:

| Bảng                      | PK runtime hiện tại | PK cần có | Kiểu              | Rows snapshot | Identity counter |
| ------------------------- | ------------------- | --------- | ----------------- | ------------: | ---------------: |
| `roles`                   | `role_id`           | `id`      | `INT IDENTITY`    |             3 |                3 |
| `users`                   | `user_id`           | `id`      | `INT IDENTITY`    |             7 |               87 |
| `seller_profiles`         | `seller_profile_id` | `id`      | `INT IDENTITY`    |             3 |               44 |
| `seller_applications`     | `application_id`    | `id`      | `INT IDENTITY`    |             2 |                2 |
| `brands`                  | `brand_id`          | `id`      | `INT IDENTITY`    |            14 |               15 |
| `layouts`                 | `layout_id`         | `id`      | `VARCHAR(50)`     |             4 |                — |
| `keyboard_kits`           | `kit_id`            | `id`      | `VARCHAR(50)`     |             9 |                — |
| `switches`                | `switch_id`         | `id`      | `VARCHAR(50)`     |            10 |                — |
| `keycap_sets`             | `keycap_id`         | `id`      | `VARCHAR(50)`     |             5 |                — |
| `stabilizers`             | `stab_id`           | `id`      | `VARCHAR(50)`     |             4 |                — |
| `accessories`             | `accessory_id`      | `id`      | `VARCHAR(50)`     |             7 |                — |
| `builds`                  | `build_id`          | `id`      | `VARCHAR(50)`     |             6 |                — |
| `build_items`             | `build_item_id`     | `id`      | `INT IDENTITY`    |            27 |              551 |
| `build_mods`              | `mod_id`            | `id`      | `INT IDENTITY`    |             6 |              136 |
| `build_requests`          | `request_id`        | `id`      | `VARCHAR(50)`     |             6 |                — |
| `devices`                 | `device_id`         | `id`      | `VARCHAR(50)`     |             2 |                — |
| `device_test_sessions`    | `session_id`        | `id`      | `VARCHAR(50)`     |            41 |                — |
| `device_key_test_results` | `key_test_id`       | `id`      | `BIGINT IDENTITY` |         3.485 |            3.752 |
| `audit_log`               | `log_id`            | `id`      | `INT IDENTITY`    |            11 |               19 |
| `chat_conversations`      | `conversation_id`   | `id`      | `VARCHAR(50)`     |             2 |                — |
| `chat_messages`           | `message_id`        | `id`      | `VARCHAR(50)`     |             4 |                — |

Các khoảng trống identity, ví dụ `users` có 7 dòng nhưng counter đã tới 87, chứng minh drop/recreate không tương đương migration. Recreate chắc chắn xóa dữ liệu hiện tại và làm mất các row ngoài seed; các ID được tạo lại không được bảo đảm giữ nguyên, còn counter/ID cấp tiếp theo sẽ thay đổi.

Inventory phụ thuộc trên database runtime:

| Đối tượng                       | Số lượng | Trạng thái                                                                                        |
| ------------------------------- | -------: | ------------------------------------------------------------------------------------------------- |
| PK clustered index              |       21 | Dùng PK cũ                                                                                        |
| FK                              |       33 | Tất cả enabled và trusted                                                                         |
| CHECK constraint                |       20 | Enabled/trusted                                                                                   |
| DEFAULT constraint              |       24 | Không đặt trên 21 PK                                                                              |
| Index trên 21 bảng nghiệp vụ    |       69 | 21 PK + 8 non-PK unique + 40 non-unique; secondary index không khai báo PK legacy làm key/include |
| View/procedure/function/trigger |        0 | Không có SQL module trong DB phải sửa text                                                        |
| Synonym                         |        0 | Không có synonym phụ thuộc tên cũ                                                                 |
| SQL Agent job step liên quan    |        0 | Không thấy job step trong `msdb` trỏ DB/tên PK cũ tại thời điểm audit                             |

Điểm này làm cho hướng rename tại chỗ khả thi hơn rebuild: FK/index ràng buộc bằng metadata cột sẽ giữ nguyên quan hệ khi đổi tên. Clustered key có thể là row locator ngầm trong nonclustered index, nhưng vẫn phụ thuộc metadata column ID chứ không phải text tên cột. Dù vậy vẫn phải diễn tập trên clone và kiểm tra lại toàn bộ metadata sau rename.

## 4. Findings chi tiết

Mức độ:

- **P0 – Chặn phát hành:** ứng dụng không thể hoạt động đúng hoặc có nguy cơ mất dữ liệu.
- **P1 – Cao:** chắc chắn gây lỗi trong một pha triển khai hoặc che giấu lỗi migration.
- **P2 – Trung bình:** rủi ro toàn vẹn, truy vết hoặc thiếu coverage cần xử lý trước khi chốt refactor.

### PKID-001 — P0 — Source code và database runtime lệch schema toàn khối

**Bằng chứng**

- Runtime DB: 21/21 PK còn tên cũ, 0 cột PK tên `id`.
- Source of truth mới: 21/21 PK là `id` trong DBML và `CreateSchema_Refactor.sql`.
- `Database/SqlServer/VerifyRefactor.sql:44-52` lỗi `Invalid column name 'id'` khi chạy trên DB hiện tại.
- `Data/SqlServer/SqlServerSettings.cs:5-6` xác nhận app dùng chính database này.

**Ảnh hưởng trực tiếp**

Các vị trí dưới đây là câu SQL hoặc chuỗi luồng bị chặn; không có nghĩa mọi statement trong toàn bộ repository đều dùng PK mới. Một số query `COUNT(*)` hoặc chỉ dùng FK vẫn chạy riêng lẻ, nhưng không cứu được luồng người dùng tổng thể.

| Khu vực            | SQL/luồng bị chặn                                                                                                         | Ảnh hưởng người dùng                                                                                                                                                                                     |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Tài khoản          | `SqlUserRepository.cs:26-38,80-92,120-169,188-235`                                                                        | Không đăng nhập/quick login/đăng ký; Admin không cập nhật, khóa hoặc đổi role                                                                                                                            |
| Seller profile     | `SqlSellerRepository.cs:35-149`                                                                                           | Admin/verified-seller list và `SaveAsync` lỗi; luồng chỉnh/xác minh tổng thể còn bị chặn qua UserRepository. `GetBySellerUserIdAsync` tự thân tương thích DB cũ nhưng sẽ lỗi sau migration theo PKID-003 |
| Seller application | `SqlSellerApplicationRepository.cs:28-33,63,76-80,113-129,176-183,216-277,322-374`                                        | Không nộp, tải chi tiết/latest, duyệt hoặc từ chối đơn seller; các query count/pending riêng lẻ vẫn có thể chạy                                                                                          |
| Catalog            | `SqlComponentRepository.cs:23-79,105-170,198-298,320-350,404-671`                                                         | Không tải/chọn hoặc quản lý đầy đủ kit, switch, keycap, stab, accessory; một số count/usage query vẫn độc lập PK                                                                                         |
| Build              | `SqlBuildRepository.cs:22-316`                                                                                            | Không tải, lưu, archive build hoặc đọc item/mod                                                                                                                                                          |
| Request            | `SqlRequestRepository.cs:24,36-49,74-142`                                                                                 | Không gửi, tải hoặc cập nhật build request; `GetCountAsync` tự thân không đọc PK                                                                                                                         |
| Device/QC          | `SqlDeviceRepository.cs:30-120`; `SqlDeviceTestSessionRepository.cs:30-162`; `SqlDeviceKeyTestResultRepository.cs:32-145` | Không tạo QC station/session và không lưu kết quả từng phím                                                                                                                                              |
| Chat               | `SqlChatRepository.cs:35-233`                                                                                             | Không tạo hội thoại, tải hoặc gửi tin nhắn                                                                                                                                                               |
| Audit              | `SqlAuditLogRepository.cs:25-85`                                                                                          | Admin không tải/ghi audit log                                                                                                                                                                            |
| Thống kê           | Các JOIN tại `SqlStatsRepository.cs:63,138,179,244-245,275,319-321`                                                       | Ba luồng thống kê — seller public stats trong Buyer dashboard, Seller dashboard và Admin overview — đều có helper lỗi                                                                                    |

**Nguyên nhân gốc**

Refactor đã được áp dụng vào file schema và phần lớn raw SQL, nhưng chưa có bước deployment/migration tương ứng cho database đang chạy.

**Hướng xử lý**

Migrate đồng bộ cả 21 PK trên database; không rollback repository về tên cũ. Chỉ đổi PK của bảng chủ thành `id`, giữ nguyên các FK có nghĩa ở bảng con.

### PKID-002 — P0 — Không có migration giữ dữ liệu; script hiện có xóa toàn bộ 21 bảng

**Bằng chứng**

- `Database/SqlServer/CreateSchema_Refactor.sql:10` gọi script là “idempotent” vì drop rồi tạo lại.
- `Database/SqlServer/CreateSchema_Refactor.sql:28-48` chạy `DROP TABLE IF EXISTS` cho cả 21 bảng.
- `Database/SqlServer/CreateSchema_Refactor.sql:12-18` tạo/dùng `CustomKeyboard_Refactor`, cũng chính là DB runtime.
- Script không có explicit transaction bao quanh toàn bộ drop/recreate và bị chia thành nhiều batch bằng `GO`; `XACT_ABORT` không biến các batch DDL auto-commit thành một migration nguyên tử.
- Không tìm thấy `sp_rename`, migration history hoặc schema version trong project.
- `Documents_Refactor/PK_To_Id_Refactor_Plan.md:209` mới chỉ ghi recreate clean; `Documents_Refactor/CUSTOM_KEYBOARD_PROJECT_OVERVIEW.md:329` vẫn để mục tạo migration chưa hoàn thành.

**Ảnh hưởng**

Chạy script này trực tiếp sẽ xóa snapshot 3.658 dòng ghi nhận ngày 2026-07-15, trong đó có 3.485 kết quả phím QC, 41 session, 6 request, 11 audit log và toàn bộ tài khoản/catalog. Identity counter cũng bị reset/reseed, không bảo toàn lịch sử cấp ID. Nếu script lỗi giữa chừng, database còn có thể bị để lại ở trạng thái drop/recreate dở dang.

**Điểm tài liệu gây hiểu nhầm**

- Comment `CreateSchema_Refactor.sql:5` nói không đụng runtime DB, nhưng runtime settings và README đều đang dùng `CustomKeyboard_Refactor`.
- README `:29-31` hướng dẫn chạy schema + seed mà không cảnh báo đây là đường clean/recreate phá dữ liệu khi chạy lại.

**Hướng xử lý**

- Tạo script migration riêng, ví dụ `MigratePkToId_20260715.sql`.
- Đổi tên/ghi rõ script hiện tại là clean-install destructive, chỉ dùng cho DB rỗng hoặc disposable.
- Bắt buộc backup + restore rehearsal trước khi động vào runtime DB.

Kiểm tra `msdb.dbo.backupset` tại thời điểm audit không thấy lịch sử backup do SQL Server quản lý cho `CustomKeyboard_Refactor`. Điều này không loại trừ file/copy backup bên ngoài, nhưng không được coi là đã có backup cho tới khi một bản full backup cụ thể được restore thử thành công.

### PKID-003 — P1 — Một câu SQL còn sót PK `seller_profile_id`, sẽ lỗi sau khi DB đã migrate

**Vị trí**

`Repositories/SqlServer/SqlSellerRepository.cs:184-194`, đặc biệt dòng 186:

```sql
SELECT seller_profile_id, ...
FROM seller_profiles
```

Sau migration bảng chỉ có PK `id`, nên câu đúng về mặt contract phải trả alias `id AS seller_profile_id` để mapper C# tiếp tục đọc property có nghĩa.

**Ảnh hưởng**

`GetBySellerUserIdAsync` tại dòng `18-28` dùng `BaseSelectSql` này. Sau khi database được migrate, các luồng sau vẫn lỗi:

- Admin chỉnh/xác minh seller: `Services/AdminService.cs:132,183,187`. Màn hình tải danh sách seller dùng query khác và không bị lỗi này sau migration.
- Tạo hội thoại Buyer–Seller hoặc Admin–Seller mới qua `EnsureVerifiedSellerAsync`: `Services/ChatService.cs:150`. List/read/send trên hội thoại đã có không phụ thuộc câu BaseSelect này.
- Luồng seller public stats: `Services/StatsService.cs:35`; đây không phải mọi query của Seller/Admin dashboard.

Đây là lỗi chắc chắn cần sửa trong cùng release với migration; nếu chỉ migrate DB thì hệ thống vẫn chưa hoàn chỉnh.

### PKID-004 — P1 — Ứng dụng không fail-fast khi schema chưa tương thích

**Bằng chứng**

- `Data/SqlServer/SqlServerHealthCheck.cs:12-16` chỉ mở connection, không kiểm tra schema/PK/version.
- Health check này không được gọi; `MainWindow.xaml.cs:19-39` khởi tạo repository trực tiếp.
- `Diagnostics/AppLog.cs:42-50` quy mọi `DbException` thành thông báo kết nối database chung.

**Ảnh hưởng**

Ứng dụng có thể mở bình thường rồi lỗi ở từng thao tác. Người dùng nhận thông báo kiểu “không thể truy cập database” dù SQL Server vẫn online; đội phát triển dễ hiểu nhầm thành nhiều bug riêng thay vì một schema drift.

**Hướng xử lý**

- Thêm `schema_version` hoặc migration history.
- Ở startup, kiểm tra version và ít nhất 21 PK bắt buộc.
- Nếu DB cũ/mixed, chặn vào ứng dụng với thông báo rõ “database chưa được migrate PK sang `id`”.
- Clean install và database nâng cấp phải cùng ghi một version contract; nếu chỉ migration tạo version, database mới tạo từ clean schema sẽ bị startup guard từ chối. Bảng version là bảng hạ tầng, không tính vào 21 bảng nghiệp vụ.

### PKID-005 — P1 — `ApplySellerApplications.sql` không thể nâng cấp DB cũ

**Vị trí:** `Database/SqlServer/ApplySellerApplications.sql:10-37`.

- Nếu `seller_applications` đã tồn tại như DB hiện tại, script in “no change” và giữ PK `application_id`.
- Nếu bảng chưa tồn tại trên DB legacy, nhánh create dùng `REFERENCES users(id)` tại dòng `24-25`; DB legacy chỉ có `users.user_id`, nên nhánh này cũng lỗi.

Script chỉ phù hợp với database đã migrate xong, không phải migration bridge. Tên/header cần làm rõ để tránh người vận hành tưởng rằng chạy file này sẽ sửa phần seller application.

### PKID-006 — P1 — `VerifyRefactor.sql` chưa đủ để xác nhận migration

Các thiếu sót tại `Database/SqlServer/VerifyRefactor.sql`:

1. Không preflight metadata 21 PK `id`, nên dừng bằng lỗi SQL thô thay vì báo schema version sai.
2. Không xác nhận 33 FK đều target `id`, enabled và trusted.
3. Row count `:12-37` hard-code theo clean seed, không phù hợp DB runtime có dữ liệu phát sinh.
4. Header nói kiểm tra mọi quan hệ tại `:107-167`, nhưng chỉ có 29/33 orphan check; thiếu:
   - `seller_applications.buyer_user_id`
   - `seller_applications.reviewed_by`
   - `chat_conversations.buyer_id`
   - `chat_conversations.admin_user_id`
5. Header `:17-18` nói `audit_log >= 3`, `seller_applications >= 1`, nhưng output `:34,37` vẫn ghi expected cố định 3 và 1.

**Hướng xử lý**

Tách verifier thành ba lớp độc lập:

- `VerifySchemaVersion`: PK/FK/type/identity/index/constraint.
- `VerifyMigrationData`: so sánh snapshot trước/sau và orphan checks.
- `VerifyCleanSeedFixture`: số dòng cố định chỉ dành cho DB test mới tạo.

### PKID-007 — P2 — Seed thiếu guard database và có ID identity hard-code trong JSON

**Seed chính**

- `Documents_Refactor/SeedData_Refactor.sql:3` nói không target runtime schema, nhưng không có `USE CustomKeyboard_Refactor` hoặc `DB_NAME()` guard.
- Tính đúng/sai phụ thuộc database context của công cụ chạy.
- File có transaction và `XACT_ABORT` tại `:7-10`, nhưng vẫn nên từ chối chạy nếu schema version chưa đúng.

**Demo analytics seed**

- Comment `Database/SqlServer/SeedDemoAnalytics_Refactor.sql:16` yêu cầu truyền `-d`, nhưng bản thân script không có database/schema guard.
- Script xóa demo rows trước tại `:24-27` và không bọc transaction; trên schema mixed có thể dừng giữa chừng.

**JSON snapshot**

- `Documents_Refactor/SeedData_Refactor.sql:345,357` hard-code `sellerUserId` và `brandId` trong JSON, trong khi row relational được resolve theo username/tên.
- Restore giữ nguyên ID/counter của bản backup. Rủi ro phát sinh khi seed được chạy trên một database mà `seller.id`/`brand.id` thực tế khác số hard-code do dữ liệu và lịch sử insert khác; khi đó JSON không khớp row relational vừa resolve.

**Hướng xử lý**

Thêm guard theo database + schema version, bọc demo seed bằng transaction, và tạo JSON từ ID vừa JOIN/resolve thay vì số identity cố định.

### PKID-008 — P2 — Upsert seller profile có thể cập nhật hai row nếu cặp ID không đồng nhất

`Repositories/SqlServer/SqlSellerRepository.cs:103-118` dùng:

```sql
WHERE id = @seller_profile_id OR user_id = @user_id
```

Trong luồng UI hiện tại, service thường nạp profile cũ rồi gán lại đúng `SellerProfileId`, nên xác suất thấp. Tuy nhiên nếu DTO stale/sai cặp, một row có thể khớp PK và row khác khớp `user_id`, khiến cả hai profile bị cập nhật.

**Hướng xử lý:** upsert theo `user_id` vì cột này có unique constraint, hoặc xác thực `id` và `user_id` cùng trỏ đúng một row rồi mới update. Kiểm tra affected rows phải đúng 1.

### PKID-009 — P2 — Contract `record_id` của `seller_profiles` chưa rõ và không nhất quán nếu kỳ vọng PK

- `Services/AdminService.cs:153-160` ghi `saved.UserId`.
- `Services/AdminService.cs:190-197` ghi `sellerUserId`.
- `table_name` lại là `seller_profiles`, có PK mới là `seller_profiles.id`.

Schema/model chưa quy định `record_id` bắt buộc luôn là PK; seed còn có ví dụ dùng username. Tuy nhiên các audit entity runtime khác chủ yếu ghi PK của bảng tương ứng. Nếu contract mong đợi PK, sau refactor cặp `(table_name, record_id)` ở hai luồng seller profile không resolve nhất quán, nhất là khi `users.id` và `seller_profiles.id` khác nhau.

**Hướng xử lý:** xác định rõ contract của `record_id`. Nếu contract là PK của `table_name`, dùng `SellerProfileId`; nếu cố ý dùng domain key `user_id`, phải ghi rõ loại key hoặc thêm cột `record_key` để tránh nhập nhằng, đồng thời cân nhắc dữ liệu audit cũ.

### PKID-010 — P2 — Coverage xác minh ID mới chưa đầy đủ

- `Documents_Refactor/SeedData_Refactor_Check.md:11-31` kết luận mọi bảng có seed coverage, nhưng danh sách thiếu `seller_applications`, `devices`, `device_test_sessions`, `device_key_test_results`.
- `seller_applications` thực tế có seed ở cuối file; ba bảng QC không có fixture trong seed chính.
- `Custom_keyboard.slnx:3-4` vẫn tham chiếu `Phase6Verification` và `WpfUiVerification`, nhưng hai project đang không tồn tại trong worktree. README `:12,19,32,382` vẫn yêu cầu chạy chúng.

**Ảnh hưởng**

Build riêng app có thể pass dù raw SQL sai. Hiện không thể chạy lại verification runner đã được README coi là release gate, và clean seed không bao phủ đường PK mới của QC.

**Hướng xử lý**

Khôi phục/cập nhật verification project hoặc tạo integration test mới chạy trên database clone đã migrate; thêm fixture tối thiểu cho device, session và key result.

## 5. Những phần đã đúng và không nên đổi

Các điểm sau đã được kiểm tra và phù hợp với thiết kế mới:

- DBML có 21 PK `id` và 33 quan hệ đều trỏ tới `table.id`.
- `CreateSchema_Refactor.sql` clean schema định nghĩa PK/FK mới nhất quán.
- Ngoài `SqlSellerRepository.BaseSelectSql`, không phát hiện thêm PK cũ bị dùng sai trong 12 repository SQL Server.
- Các SELECT kiểu `id AS user_id`, `id AS build_id`, `id AS request_id` là đúng để mapper/domain model giữ tên có nghĩa.
- FK ở bảng con như `role_id`, `user_id`, `build_id`, `request_id`, `conversation_id` phải giữ nguyên; chúng không phải PK bị sót.
- Property/parameter C# như `UserId`, `BuildId`, `KitId`, `@user_id` nên giữ nguyên. Không đổi hàng loạt thành `Id`.
- Kiểu C# và SQL mới khớp: PK chuỗi dùng `VARCHAR(50)`/`string`; identity `INT` dùng `int`; key-test identity `BIGINT` dùng `long`.
- Dynamic SQL của component lấy table từ enum whitelist và hiện dùng cột PK `id` đúng.
- Database runtime không có view/procedure/function/trigger chứa SQL text cũ; đây là điều kiện thuận lợi cho rename tại chỗ.

## 6. Phương án giải quyết được khuyến nghị

### 6.1 Quyết định đường migration

| Điều kiện                                             | Phương án                                                       |
| ----------------------------------------------------- | --------------------------------------------------------------- |
| Cần giữ tài khoản, build, request, QC, audit hiện tại | **Migration in-place bằng rename; khuyến nghị cho DB hiện tại** |
| DB hoàn toàn disposable và đã được phê duyệt xóa      | Backup rồi clean recreate + seed                                |

Với snapshot 3.658 dòng ngày 2026-07-15 và identity counter đã phát sinh nhiều khoảng trống, báo cáo khuyến nghị **in-place migration**. Preflight ngày triển khai phải chụp baseline mới.

### 6.2 Artifact cần tạo trước khi triển khai

1. `MigratePkToId_20260715.sql`: migration một chiều, có guard và transaction; ghi schema version trong chính transaction sau khi internal assertions pass.
2. `VerifyPkToId_Preflight.sql`: chỉ đọc, dừng nếu DB không đúng trạng thái cũ dự kiến.
3. `VerifyPkToId_Postflight.sql`: chỉ đọc, xác nhận schema + data sau rename.
4. Release candidate bất biến đã sửa `SqlSellerRepository.BaseSelectSql`, được package và ghi hash.
5. Schema version/migration history dùng chung cho cả clean install và upgraded DB, ví dụ version `2026.07.15-pk-id`.
6. Previous/rollback binary + config tương thích schema cũ, cũng được package và ghi hash.
7. Manifest hash cho release binary và toàn bộ migration/verify script; runbook backup, restore rehearsal, rollout và rollback.

### 6.3 Preflight bắt buộc

Trên bản restore clone, migration phải từ chối chạy nếu bất kỳ điều kiện nào sai:

- Đúng database name/instance đã chọn.
- Đủ 21 bảng nghiệp vụ; bảng hạ tầng migration/version được kiểm kê riêng.
- Xác nhận đủ cả 21 mapping trước lệnh rename đầu tiên: cột PK cũ phải tồn tại và `id` chưa tồn tại.
- Mỗi PK là single-column, đúng type, length và identity flag.
- Có đúng 33 FK; `is_disabled = 0`, `is_not_trusted = 0`.
- Deployment principal có quyền `ALTER` cần thiết trên đủ 21 bảng cho `sp_rename` ([Microsoft Learn – sp_rename permissions](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-rename-transact-sql?view=sql-server-ver17#permissions)).
- Không có view/procedure/function/trigger/synonym hoặc SQL Agent job step tham chiếu tên cũ; tiếp tục kiểm kê script/integration bên ngoài DB.
- Chụp và lưu row count của 21 bảng.
- Chụp PK key set/min/max, fingerprint phù hợp cho từng bảng và 9 identity counter hiện tại.
- Có full backup đã kiểm tra restore thành công.
- Ứng dụng/MQTT subscriber đã dừng để không có write trong lúc đổi schema.

Nếu thấy trạng thái mixed, ví dụ một số bảng đã có `id` nhưng bảng khác chưa đổi, **không tự đoán và không tiếp tục**; cần điều tra lịch sử lần chạy trước.

### 6.4 Cấu trúc migration an toàn

Migration nên có `SET XACT_ABORT ON`, `TRY/CATCH`, transaction và guard cho từng mapping. Ví dụ cấu trúc, không phải script hoàn chỉnh:

```sql
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Phase A: validate all 21 mappings/types/identity/FK state first.
    IF COL_LENGTH('dbo.users', 'user_id') IS NULL
       OR COL_LENGTH('dbo.users', 'id') IS NOT NULL
        THROW 51000, 'Unexpected users PK state.', 1;

    -- ...19 checks khác và check mapping thứ 21...

    -- Phase B only starts after every preflight assertion passed.
    EXEC sys.sp_rename N'dbo.users.user_id', N'id', N'COLUMN';

    -- Lặp lại đủ 21 rename; chạy internal post-rename assertions.
    -- Ghi schema version mới trong cùng transaction sau các assertions.

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

Nguyên tắc:

- Fully qualify `dbo.table.old_pk` cho từng lệnh.
- Validate toàn bộ 21 mapping/type/identity/quyền trước lệnh `sp_rename` đầu tiên; không guard/rename xen kẽ.
- Chỉ rename 21 PK, không rename FK ở bảng con.
- Không có DML lên 21 bảng nghiệp vụ; không drop/recreate table hoặc copy dữ liệu sang bảng mới. DML duy nhất được phép là ghi bảng hạ tầng migration/version sau khi assertions pass.
- Không reset/reseed identity.
- Không cần đổi tên cosmetic của constraint/index; tên `_id` trên index FK vẫn đúng nghĩa.
- Chạy toàn bộ 21 rename trong cùng maintenance window và cùng transaction được diễn tập.

SQL Server không tự cập nhật SQL text trong view/module khi rename cột. Database hiện tại không có module loại này, nhưng repository, script, job ngoài DB và integration test vẫn phải được rà lại. Tham khảo: [Microsoft Learn – Rename columns](https://learn.microsoft.com/en-us/sql/relational-databases/tables/rename-columns-database-engine?view=sql-server-ver17).

### 6.5 Postflight schema và data

Trước khi mở lại ứng dụng, tất cả điều kiện sau phải pass:

| Kiểm tra            | Điều kiện pass                                                               |
| ------------------- | ---------------------------------------------------------------------------- |
| PK mới              | 21/21 PK có tên `id`                                                         |
| PK legacy           | 0/21 tên cũ còn là cột trên bảng chủ                                         |
| FK target           | 33/33 FK tham chiếu PK `id`                                                  |
| FK state            | Disabled = 0, untrusted = 0                                                  |
| Type/identity       | Không thay đổi so với snapshot                                               |
| Identity counter    | 9/9 counter giữ nguyên                                                       |
| Row count           | 21/21 bảng bằng snapshot trước migration                                     |
| PK min/max          | Không thay đổi                                                               |
| Key set/fingerprint | Không thay đổi theo snapshot đã chọn                                         |
| Constraint          | `DBCC CHECKCONSTRAINTS` không trả lỗi                                        |
| Orphan              | 33/33 quan hệ có orphan count = 0                                            |
| Schema version      | Postflight chỉ đọc và xác nhận version mà migration đã ghi trong transaction |

Không dùng các row count clean seed trong `VerifyRefactor.sql` làm tiêu chí mất/không mất dữ liệu. Tiêu chí đúng là so với snapshot ngay trước migration. Row count, min/max, identity và orphan là sanity checks, không tự chứng minh toàn bộ nội dung row không đổi; vì vậy migration phải không có DML trên bảng nghiệp vụ và nên bổ sung key-set/fingerprint hoặc cơ chế snapshot so sánh thích hợp ngoài full backup.

### 6.6 Smoke test chức năng bắt buộc

| Nhóm               | Kịch bản tối thiểu                                                                    |
| ------------------ | ------------------------------------------------------------------------------------- |
| Account            | Login Buyer/Seller/Admin; quick login; đăng ký Buyer mới                              |
| User/Admin         | Tải user, khóa/mở, đổi role                                                           |
| Seller             | Tải profile bằng user ID; tạo/sửa profile; verify/unverify                            |
| Seller application | Submit, xem latest, approve, reject                                                   |
| Catalog            | Tải list/by-id; admin thêm/sửa/ẩn/khôi phục brand/layout/component                    |
| Build              | Tạo, tải, sửa, archive; lưu item và mod                                               |
| Request            | Gửi request, seller tải/cập nhật trạng thái, kiểm tra ownership                       |
| Chat               | Tạo conversation, tải list, gửi/tải message                                           |
| QC                 | Tạo device, session, ghi key result, complete session, đọc summary                    |
| Stats              | Ba luồng: seller public stats trong Buyer dashboard; Seller dashboard; Admin overview |
| Audit              | Ghi/đọc log; xác nhận `record_id` seller profile theo contract mới                    |

Smoke test seller profile phải chạy sau khi sửa PKID-003; nếu không, migration DB có thể pass metadata nhưng ứng dụng vẫn lỗi.

Phải tách hai mức kiểm tra:

- **Clone/full smoke:** cho phép write và cleanup; chạy đầy đủ chuỗi cùng một bộ ID: tạo/lấy Build → tạo Request → `Pending` → `Accepted` → `In_progress` → tạo QC session + đủ key results → complete session → complete request → xác nhận stats.
- **Production/safe smoke:** ưu tiên read-only. Nếu bắt buộc write, dùng account/record test định danh, quy định cleanup và chấp nhận rõ tác động còn lại lên audit/stats/QC; không tạo hàng trăm key result tùy tiện trên dữ liệu thật.

Trước migration cần chọn và lưu một số ID lịch sử đại diện của build/request/session/key result. Sau migration phải tải lại chính các ID đó và so sánh KPI stats trước/sau; đây là kiểm tra trực tiếp cho việc alias PK mới và FK cũ vẫn cùng trỏ một thực thể.

## 7. Thứ tự triển khai đề xuất

1. Chốt mapping 21 PK và đóng băng thay đổi schema liên quan.
2. Sửa các lỗi code hậu-migration: tối thiểu PKID-003; quyết định PKID-008/009.
3. Viết preflight, migration, postflight và integration smoke test.
4. Build/package release candidate bất biến và previous/rollback artifact; tạo manifest/hash cho binary, config và mọi SQL script. Không rebuild sau khi rehearsal pass.
5. Full backup database runtime; restore sang clone.
6. Dùng đúng các artifact đã hash để diễn tập đầy đủ trên clone, bao gồm deploy, full smoke, rollback binary/config và restore/so sánh snapshot.
7. Mở maintenance window; dừng app và mọi writer.
8. Backup lần cuối; chạy preflight.
9. Chạy migration; chạy postflight và business invariant verify.
10. Deploy đúng release candidate/hash đã pass rehearsal; chạy production-safe smoke theo ba role, rồi đối chiếu các ID/KPI lịch sử. Full mutating E2E phải đã pass trên clone.
11. Chỉ mở lại hệ thống khi toàn bộ release gate pass.
12. Theo dõi log `Invalid column name`, lỗi FK và lỗi mapper trong giai đoạn sau triển khai.

Không để code mới chạy với DB cũ, cũng không để DB mới chạy với bản app còn `seller_profile_id` ở `BaseSelectSql`.

## 8. Rollback

### Trước commit migration

- Bất kỳ assertion nào sai phải `THROW` và rollback transaction.
- Không cố sửa tay từng bảng trong maintenance window.

### Sau commit nhưng trước mở hệ thống

- Nếu postflight hoặc smoke test lỗi nghiêm trọng, giữ maintenance mode.
- Ưu tiên restore full backup đã diễn tập thay vì rename ngược thủ công không kiểm soát.
- Deploy lại đúng previous/rollback binary + config tương thích với schema được restore; artifact này phải đã được lưu hash và thử trên clone trước maintenance window.

### Sau khi đã mở hệ thống

- Không restore đè nếu đã có write mới mà chưa đánh giá mất dữ liệu.
- Dừng writer, chụp tail data, lập kế hoạch merge/restore riêng.
- Vì vậy smoke test đầy đủ trước khi mở lại là release gate bắt buộc.

## 9. Danh sách việc cần làm theo ưu tiên

| Ưu tiên | Việc                                                 | Phụ thuộc             |
| ------- | ---------------------------------------------------- | --------------------- |
| P0      | Backup + restore rehearsal                           | Không                 |
| P0      | Viết migration rename đủ 21 PK, có pre/post guard    | Mapping trong báo cáo |
| P0      | Không dùng clean schema trên DB cần giữ dữ liệu      | Quyết định vận hành   |
| P1      | Sửa `SqlSellerRepository.BaseSelectSql`              | Trước release app mới |
| P1      | Thêm schema version và startup fail-fast             | Migration version     |
| P1      | Tách verifier schema/data/clean-seed, đủ 33 FK       | Migration artifact    |
| P1      | Làm rõ hoặc giới hạn `ApplySellerApplications.sql`   | Schema version        |
| P2      | Làm chặt upsert seller và contract audit `record_id` | Quyết định domain     |
| P2      | Thêm database guard/transaction cho seed             | Schema version        |
| P2      | Khôi phục integration/UI verification và seed QC     | DB clone đã migrate   |

## 10. Kết luận cuối

Vấn đề lớn nhất không nằm ở model C# hay việc vẫn dùng tên `UserId`/`BuildId`; các tên đó là đúng ở tầng domain. Vấn đề thực tế là **deployment database chưa theo refactor source code**.

Để hoàn tất refactor an toàn cần xử lý như một release migration nguyên tử:

1. Bảo toàn database hiện tại bằng backup + migration in-place đủ 21 PK.
2. Sửa câu SQL seller còn sót trước khi phát hành.
3. Bổ sung schema guard và verification đủ để không tái diễn schema drift.

Cho tới khi ba điều kiện này hoàn thành, trạng thái nên được xem là **P0 / chưa sẵn sàng chạy với database hiện tại**.
