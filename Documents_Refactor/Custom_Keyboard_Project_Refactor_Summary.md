# Custom Keyboard Builder - Project Refactor Summary

## 1. Muc dich tai lieu

Tai lieu nay tong hop toan bo tinh trang refactor cua du an Custom Keyboard Builder:

- Du an dang lam gi.
- Da tao nhung tai lieu/diagram nao.
- Dang chuyen he thong tu mo hinh cu sang mo hinh moi nhu the nao.
- Thu tu doc va doi chieu ERD, FHD, Use Cases, DFD, Class Diagram.
- Huong di cho SQL schema, SQL migration, seed dataset.
- Dataset hien tai phu hop voi nhung bang nao trong ERD moi.

File nay dung nhu tai lieu dinh huong truoc khi bat dau refactor code that su.

## 2. Du an dang lam gi

Du an la he thong Custom Keyboard Builder. Muc tieu chinh la cho buyer tao cau hinh ban phim custom dua tren keyboard kit, sau do chon cac linh kien can thiet va gui request cho seller de xu ly build.

Trong ban refactor hien tai, he thong tap trung vao flow thuc te va gon hon:

- Buyer chon keyboard kit lam nen tang.
- Buyer chon switch, keycap set, stabilizer package, accessory neu can.
- He thong kiem tra tinh hop ly cua build: kit, switch, keycap, stab, accessory, gia snapshot.
- Buyer luu build hoac gui request cho seller.
- Seller xu ly request theo trang thai.
- Admin quan tri user, seller, catalog va audit.
- Buyer/seller/admin trao doi qua chat trong pham vi request hoac seller verification.

Huong refactor nay co tinh chat "kit-shop realistic": keyboard kit da gom case, PCB, plate va cac phan co ban. Vi vay du an khong tach case/PCB/plate thanh cac bang lua chon rieng nua.

## 3. Da lam gi trong Documents_Refactor

Thu muc chinh hien tai:

```text
Documents_Refactor/
```

Nhung file da tao/hoan thien:

| File | Vai tro |
| --- | --- |
| `Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml` | ERD moi, nguon su that chinh cho schema refactor |
| `Keyboard_Build_Validation_Logic.md` | Logic kiem tra tinh hoan thien va hop ly cua build |
| `Custom_Keyboard_FHD_Refactor.md` | Functional Hierarchy Diagram moi, chi dung muc x.x |
| `Custom_Keyboard_Use_Cases_Refactor.md` | Use cases cho 3 role: Buyer, Seller, Admin |
| `Custom_Keyboard_DFD_Context_Level0_Refactor.md` | DFD context va level 0 |
| `Custom_Keyboard_DFD_Level1_Refactor.md` | DFD level 1 cho cac tien trinh chinh |
| `Custom_Keyboard_DFD_Level2_Refactor.md` | DFD level 2 cho cac tien trinh quan trong |
| `Custom_Keyboard_Class_Diagram_Refactor.md` | Class diagram phuc vu refactor code |
| `SeedData_Refactor.sql` | SQL seed dataset theo ERD moi |
| `SeedData_Refactor_Check.md` | Ket qua check cheo dataset voi ERD moi |
| `Custom_Keyboard_Project_Refactor_Summary.md` | File tong hop hien tai |

## 4. Dinh huong refactor: tu cu sang moi

### 4.1 Mo hinh cu

Mo hinh cu trong `Documents/` co nhieu diagram va ERD lien quan den:

- Component catalog tach nhieu bang chi tiet.
- Case, PCB, plate, layout rules, compatibility rules rieng.
- Inventory/seller inventory.
- Mot so logic build qua chi tiet so voi muc tieu hien tai.
- Diagram co nhieu muc x.x.x va nhieu lien ket, kho bat dau code refactor.

Nhung tai lieu cu van co gia tri de tham khao flow, nhung khong con la nguon schema chinh cho refactor.

### 4.2 Mo hinh moi

Mo hinh moi trong `Documents_Refactor/` rut gon theo nguyen tac:

- ERD khong vuot qua 25 lien ket. Hien tai link count la 24.
- Khong tach case, PCB, plate thanh bang rieng.
- Khong dung seller inventory trong giai do refactor nay.
- Keyboard kit la san pham nen tang, da gom case/PCB/plate/foam/cable neu co.
- Build chi can tro den kit va danh sach item bo sung.
- Stabilizer duoc toi gian thanh package theo layout/form factor.
- Keycap chi can check form factor o muc don gian, khong check tung nut chi tiet.
- Seller khong ban theo stock rieng; seller nhan request build.
- Gia duoc luu snapshot tai thoi diem buyer tao/gui request.

## 5. ERD moi la nguon chuan

File ERD:

```text
Documents_Refactor/Custom_Keyboard_ERD_Realistic_Kit_Shop_Proposal.dbml
```

Thong tin chinh:

- Tong bang: 17.
- Tong lien ket: 24.
- Khong co seller inventory.
- Khong co case/PCB/plate rieng.
- Build duoc tach thanh `builds` va `build_items`.
- Request duoc quan ly bang `build_requests`.
- Chat duoc quan ly bang `chat_conversations` va `chat_messages`.
- Audit duoc quan ly bang `audit_log`.

### 5.1 Danh sach bang ERD moi

| Nhom | Bang | Muc dich |
| --- | --- | --- |
| Account | `roles` | Dinh nghia Buyer, Seller, Admin |
| Account | `users` | Tai khoan nguoi dung |
| Account | `seller_profiles` | Ho so seller va trang thai verify |
| Catalog | `brands` | Thuong hieu |
| Catalog | `layouts` | Layout/form factor/key count |
| Catalog | `keyboard_kits` | Kit nen tang cua ban phim |
| Catalog | `switches` | Switch theo cong nghe va mount |
| Catalog | `keycap_sets` | Bo keycap theo form factor |
| Catalog | `stabilizers` | Goi stabilizer co ban theo layout |
| Catalog | `accessories` | Phu kien, lube, film, cable, foam, tool |
| Build | `builds` | Build cua buyer, chua kit va tong snapshot |
| Build | `build_items` | Cac item trong build: switch/keycap/stab/accessory |
| Build | `build_mods` | Cac yeu cau mod: lube, film, tune, calibration |
| Request | `build_requests` | Request buyer gui cho seller |
| Admin | `audit_log` | Lich su thay doi quan trong |
| Chat | `chat_conversations` | Cuoc hoi thoai seller-buyer hoac seller-admin |
| Chat | `chat_messages` | Tin nhan trong conversation |

### 5.2 Nhung bang/khai niem cu can tranh dua lai

Trong giai do refactor nay, khong dua lai cac khai niem sau vao schema chinh:

- `seller_inventory`
- Stock rieng theo seller
- `case_id`, `pcb_id`, `plate_id` trong build
- Bang case/PCB/plate rieng
- Layout rule qua chi tiet
- Stabilizer theo tung chieu dai space/shift/enter
- Keycap compatibility theo tung key unit
- Switch mod columns cu nhu `lube_type`, `is_filmed`, `spring_weight_g`

## 6. FHD moi

File:

```text
Documents_Refactor/Custom_Keyboard_FHD_Refactor.md
```

FHD moi chi giu den muc x.x, khong dung x.x.x. Cac muc sau se nam trong use cases.

Nhom chuc nang chinh:

| Nhom | Chuc nang |
| --- | --- |
| 1. Tai khoan | Dang ky, dang nhap, quan ly profile, seller profile |
| 2. Buyer - Tao build tu kit | Xem catalog, chon kit, cau hinh build, tinh gia, luu build, gui request |
| 3. Seller - Xu ly request | Xem request, cap nhat trang thai, chat voi buyer |
| 4. Admin - Quan tri he thong | Quan ly user, seller, catalog, audit |
| 5. Chat | Tao conversation, gui/nhan tin nhan, link voi request neu can |

FHD moi phai duoc dung de kiem tra xem code refactor co thieu man hinh/service nao khong.

## 7. Use cases moi

File:

```text
Documents_Refactor/Custom_Keyboard_Use_Cases_Refactor.md
```

Use cases duoc gom theo 3 role:

| Role | Use case chinh |
| --- | --- |
| Buyer | Tao build tu kit va gui request |
| Seller | Xu ly request build |
| Admin | Quan tri he thong |

Chat khong duoc tach thanh role rieng. Chat la chuc nang phu tro cua Buyer, Seller va Admin.

Use cases la noi mo ta chi tiet cac buoc ma FHD khong ghi x.x.x nua.

## 8. DFD moi

DFD duoc chia thanh 3 file de de doc va de doi chieu.

| File | Noi dung |
| --- | --- |
| `Custom_Keyboard_DFD_Context_Level0_Refactor.md` | Context diagram va Level 0 |
| `Custom_Keyboard_DFD_Level1_Refactor.md` | Level 1 cho 5 process chinh |
| `Custom_Keyboard_DFD_Level2_Refactor.md` | Level 2 cho build config, request status, chat message |

### 8.1 Cac tac nhan ngoai

- Buyer
- Seller
- Admin

### 8.2 Cac process level 0

| Process | Ten | Lien quan |
| --- | --- | --- |
| 1.0 | Quan ly tai khoan | `roles`, `users`, `seller_profiles` |
| 2.0 | Quan ly build keyboard | `brands`, `layouts`, `keyboard_kits`, `switches`, `keycap_sets`, `stabilizers`, `accessories`, `builds`, `build_items`, `build_mods` |
| 3.0 | Quan ly request build | `build_requests`, `builds`, `users` |
| 4.0 | Quan tri he thong | `users`, `seller_profiles`, catalog tables, `audit_log` |
| 5.0 | Quan ly chat | `chat_conversations`, `chat_messages`, `build_requests` |

### 8.3 Level 2 quan trong

DFD Level 2 hien tai tap trung vao:

- 2.2 Xu ly cau hinh build.
- 3.4 Cap nhat trang thai request.
- 5.3 Luu tin nhan.

Day la cac process can code can than vi co lien quan truc tiep den tinh hop ly du lieu.

## 9. Class diagram phuc vu code

File:

```text
Documents_Refactor/Custom_Keyboard_Class_Diagram_Refactor.md
```

Class diagram duoc chia thanh:

- Domain Model Class Diagram.
- Service va Repository Class Diagram.
- MVVM/ViewModel Class Diagram.
- Logic validation trong BuildService.
- Mapping voi FHD.
- Cac class cu can loai bo/thay the.

### 9.1 Domain model chinh

Class nen co theo ERD:

- `Role`
- `User`
- `SellerProfile`
- `Brand`
- `Layout`
- `KeyboardKit`
- `Switch`
- `KeycapSet`
- `Stabilizer`
- `Accessory`
- `Build`
- `BuildItem`
- `BuildMod`
- `BuildRequest`
- `AuditLog`
- `ChatConversation`
- `ChatMessage`

### 9.2 Vi sao co `BuildItem`

`BuildItem` can thiet vi build co nhieu loai item va moi item co quantity/price snapshot rieng:

- Switch can quantity theo kit, vi du 70/85/90.
- Keycap set thuong quantity la 1.
- Stabilizer package thuong quantity la 1.
- Accessory co the quantity la 1 hoac nhieu hon.

Neu dua tat ca vao `builds`, bang `builds` se phinh to, kho mo rong va kho tinh tong theo item. Vi vay `builds` chi giu kit, buyer, status, tong snapshot; `build_items` giu cac item bo sung.

### 9.3 Phu kien noi vao dau

Trong ERD moi, accessory nam trong `build_items`, khong noi truc tiep vao `builds`.

Ly do:

- Accessory cung la mot item co quantity va price snapshot.
- Accessory co the gan voi switch/stab/kit/general qua `target_component`.
- `build_items` giu chung moi dong san pham ma buyer da chon.

Ve UI, buyer van nhin accessory la phu kien cua build. Ve database, no la item trong build.

## 10. Logic validation can giu

File:

```text
Documents_Refactor/Keyboard_Build_Validation_Logic.md
```

Logic chinh:

| Nhom check | Noi dung |
| --- | --- |
| Thong tin co ban | Build co buyer, name, status hop le |
| Kit | Kit ton tai, available, co layout |
| Switch | Switch ton tai, available, technology/mount phu hop kit |
| Quantity switch | Tong switch quantity phai bang `keyboard_kits.required_switch_quantity` |
| Keycap | Keycap available, form factor phu hop layout |
| Stabilizer | Stabilizer package phu hop layout |
| Accessory | Accessory available, target component hop le |
| Seller | Seller active, role Seller, profile verified |
| Gia | Tong snapshot = kit price + sum item quantity * unit price |
| Request | Status flow hop le: Pending, Accepted, In_progress, Completed, Cancelled |
| Chat | Conversation buyer-seller/admin-seller dung pham vi |

### 10.1 Error va warning nen tach

Error:

- Thieu kit.
- Switch khong phu hop kit.
- Switch quantity sai.
- Seller khong verified.
- Build request tro den build khong ton tai.
- Conversation co ca buyer va admin hoac khong co ca hai.

Warning:

- Build draft thieu keycap/stabilizer.
- Accessory khong co target component ro rang.
- Item unavailable nhung build archived van can hien thi.
- Gia snapshot khac gia catalog hien tai.

## 11. SQL schema va migration nen di nhu the nao

Hien tai da co DBML va seed dataset refactor. Chua nen chay seed vao database runtime cu neu database chua duoc migrate sang ERD moi.

Thu tu dung SQL nen la:

1. Chot ERD trong file DBML.
2. Tu DBML tao SQL schema moi hoac migration SQL.
3. Kiem tra schema SQL co dung 17 bang va 24 lien ket khong.
4. Tao/backup database test rieng cho refactor.
5. Chay schema SQL tren database test.
6. Chay `SeedData_Refactor.sql`.
7. Chay cac cau query check:
   - Dem so dong tung bang.
   - Check FK orphan.
   - Check build total snapshot.
   - Check switch quantity theo kit.
   - Check seller request chi den seller verified.
   - Check conversation chi co buyer hoac admin, khong phai ca hai.
8. Sau khi SQL pass moi bat dau refactor code/repository/service.

### 11.1 Thu tu tao bang schema

Khi viet SQL schema, nen tao bang theo thu tu:

1. `roles`
2. `users`
3. `seller_profiles`
4. `brands`
5. `layouts`
6. `keyboard_kits`
7. `switches`
8. `keycap_sets`
9. `stabilizers`
10. `accessories`
11. `builds`
12. `build_items`
13. `build_mods`
14. `build_requests`
15. `audit_log`
16. `chat_conversations`
17. `chat_messages`

Ly do: cac bang phu thuoc FK phai tao sau bang cha.

### 11.2 Thu tu drop bang neu can reset DB test

Neu reset database test, drop theo thu tu nguoc lai:

1. `chat_messages`
2. `chat_conversations`
3. `audit_log`
4. `build_requests`
5. `build_mods`
6. `build_items`
7. `builds`
8. `accessories`
9. `stabilizers`
10. `keycap_sets`
11. `switches`
12. `keyboard_kits`
13. `layouts`
14. `brands`
15. `seller_profiles`
16. `users`
17. `roles`

## 12. Dataset refactor

File:

```text
Documents_Refactor/SeedData_Refactor.sql
```

File check:

```text
Documents_Refactor/SeedData_Refactor_Check.md
```

Dataset phu hop voi ERD moi va da duoc check tinh logic.

### 12.1 Bang va dataset hien co

| Bang | So dong seed | Noi dung test |
| --- | ---: | --- |
| `roles` | 3 | Buyer, Seller, Admin |
| `users` | 7 | Admin, buyer active/inactive, seller verified/unverified |
| `seller_profiles` | 3 | 2 seller verified, 1 seller pending |
| `brands` | 13 | Brand cho kit/switch/keycap/stab |
| `layouts` | 4 | 65, 75, TKL, 100 |
| `keyboard_kits` | 9 | Mechanical, HE, available/unavailable |
| `switches` | 10 | MX 3-pin, MX 5-pin, HE, available/unavailable |
| `keycap_sets` | 5 | Universal va layout-limited |
| `stabilizers` | 4 | Stabilizer package theo layout group |
| `accessories` | 7 | Lube, film, cable, foam, tool |
| `builds` | 5 | Draft, Saved, Requested, Archived |
| `build_items` | 17 | Item switch/keycap/stab/accessory |
| `build_mods` | 5 | Lube, tune, calibration, film |
| `build_requests` | 2 | Pending va Completed |
| `audit_log` | 3 | Seller verify, catalog seed, request status |
| `chat_conversations` | 2 | Buyer-seller va admin-seller |
| `chat_messages` | 4 | Tin nhan test |

### 12.2 Cac build mau trong dataset

| Build | Trang thai | Muc dich |
| --- | --- | --- |
| `BUILD_REF_NEO65_MECH` | Saved | Build mechanical 65% hop le |
| `BUILD_REF_BOOG75_HE` | Requested | Build HE 75% co request pending |
| `BUILD_REF_QK65_THOCK` | Requested | Build mechanical 65% co request completed |
| `BUILD_REF_AULA_DRAFT` | Draft | Build thieu keycap/stab de test warning UI |
| `BUILD_REF_ARCHIVED_NEO80` | Archived | Build archived de test filter/history |

### 12.3 Tong tien build da check

| Build | Tong snapshot |
| --- | ---: |
| `BUILD_REF_NEO65_MECH` | 225.30 |
| `BUILD_REF_BOOG75_HE` | 384.00 |
| `BUILD_REF_QK65_THOCK` | 368.60 |
| `BUILD_REF_AULA_DRAFT` | 89.30 |
| `BUILD_REF_ARCHIVED_NEO80` | 349.40 |

Gia trong dataset la sample snapshot de test/refactor, khong phai live market price.

## 13. Thu tu doc/chay diagram va SQL khi refactor

Nen lam theo thu tu sau:

1. Doc file tong hop nay.
2. Doc ERD moi de nam schema.
3. Doc validation logic de biet rule nao can code.
4. Doc FHD de nam chuc nang cap cao.
5. Doc Use Cases de nam luong theo role.
6. Doc DFD Context/Level 0 de nam bien he thong va data store.
7. Doc DFD Level 1 de nam flow tung process.
8. Doc DFD Level 2 de nam flow chi tiet build/request/chat.
9. Doc Class Diagram de map sang code.
10. Tao SQL schema/migration tu ERD.
11. Tao database test refactor.
12. Chay schema SQL.
13. Chay `SeedData_Refactor.sql`.
14. Chay query check du lieu.
15. Bat dau refactor code theo class diagram/service/repository.

## 14. Cac query check nen co sau khi chay seed

Khi schema refactor da san sang, nen viet mot file SQL check rieng voi cac nhom query:

### 14.1 Check so dong

```sql
SELECT 'roles' AS table_name, COUNT(*) AS row_count FROM roles
UNION ALL SELECT 'users', COUNT(*) FROM users
UNION ALL SELECT 'seller_profiles', COUNT(*) FROM seller_profiles
UNION ALL SELECT 'brands', COUNT(*) FROM brands
UNION ALL SELECT 'layouts', COUNT(*) FROM layouts
UNION ALL SELECT 'keyboard_kits', COUNT(*) FROM keyboard_kits
UNION ALL SELECT 'switches', COUNT(*) FROM switches
UNION ALL SELECT 'keycap_sets', COUNT(*) FROM keycap_sets
UNION ALL SELECT 'stabilizers', COUNT(*) FROM stabilizers
UNION ALL SELECT 'accessories', COUNT(*) FROM accessories
UNION ALL SELECT 'builds', COUNT(*) FROM builds
UNION ALL SELECT 'build_items', COUNT(*) FROM build_items
UNION ALL SELECT 'build_mods', COUNT(*) FROM build_mods
UNION ALL SELECT 'build_requests', COUNT(*) FROM build_requests
UNION ALL SELECT 'audit_log', COUNT(*) FROM audit_log
UNION ALL SELECT 'chat_conversations', COUNT(*) FROM chat_conversations
UNION ALL SELECT 'chat_messages', COUNT(*) FROM chat_messages;
```

### 14.2 Check build total snapshot

```sql
SELECT
    b.build_id,
    b.total_cost_snapshot,
    CAST(k.price_usd + SUM(bi.quantity * bi.unit_price_snapshot) AS decimal(10,2)) AS calculated_total
FROM builds b
INNER JOIN keyboard_kits k ON k.kit_id = b.kit_id
LEFT JOIN build_items bi ON bi.build_id = b.build_id
GROUP BY b.build_id, b.total_cost_snapshot, k.price_usd
HAVING b.total_cost_snapshot <> CAST(k.price_usd + SUM(bi.quantity * bi.unit_price_snapshot) AS decimal(10,2));
```

Expected result: 0 rows.

### 14.3 Check switch quantity

```sql
SELECT
    b.build_id,
    k.required_switch_quantity,
    SUM(CASE WHEN bi.switch_id IS NOT NULL THEN bi.quantity ELSE 0 END) AS selected_switch_quantity
FROM builds b
INNER JOIN keyboard_kits k ON k.kit_id = b.kit_id
LEFT JOIN build_items bi ON bi.build_id = b.build_id
WHERE b.status IN ('Saved', 'Requested')
GROUP BY b.build_id, k.required_switch_quantity
HAVING SUM(CASE WHEN bi.switch_id IS NOT NULL THEN bi.quantity ELSE 0 END) <> k.required_switch_quantity;
```

Expected result: 0 rows for complete/saved/requested builds.

### 14.4 Check exactly one product FK in build_items

```sql
SELECT *
FROM build_items
WHERE
    (CASE WHEN switch_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN keycap_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN stab_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN accessory_id IS NULL THEN 0 ELSE 1 END) <> 1;
```

Expected result: 0 rows.

### 14.5 Check seller request chi den seller verified

```sql
SELECT br.request_id, u.username, sp.is_verified, u.is_active
FROM build_requests br
INNER JOIN users u ON u.user_id = br.seller_user_id
LEFT JOIN seller_profiles sp ON sp.user_id = u.user_id
WHERE u.is_active = 0 OR sp.is_verified = 0 OR sp.seller_profile_id IS NULL;
```

Expected result: 0 rows.

### 14.6 Check conversation buyer/admin rule

```sql
SELECT *
FROM chat_conversations
WHERE
    (CASE WHEN buyer_id IS NULL THEN 0 ELSE 1 END) +
    (CASE WHEN admin_user_id IS NULL THEN 0 ELSE 1 END) <> 1;
```

Expected result: 0 rows.

## 15. Huong refactor code

Sau khi SQL schema va dataset pass, code nen refactor theo thu tu:

1. Entity/domain model theo ERD moi.
2. DbContext/repository theo 17 bang moi.
3. Catalog service:
   - Brands
   - Layouts
   - Keyboard kits
   - Switches
   - Keycap sets
   - Stabilizers
   - Accessories
4. Build service:
   - Tao build
   - Them/sua/xoa build item
   - Tinh total snapshot
   - Validate switch/keycap/stab/accessory
   - Luu draft/saved/archive
5. Request service:
   - Gui request
   - Seller cap nhat status
   - Luu payload snapshot
6. Chat service:
   - Tao conversation
   - Gui message
   - Kiem tra sender nam trong conversation
7. Admin service:
   - Quan ly user/seller
   - Quan ly catalog
   - Ghi audit log
8. ViewModel/UI:
   - Buyer build configurator
   - Seller request dashboard
   - Admin management
   - Chat panel

## 16. Nguyen tac khong de refactor bi lech

Khi code hoac sua SQL, can giu cac nguyen tac:

- ERD moi la nguon chuan.
- Khong them lien ket moi neu khong that su can.
- Khong dua inventory vao lai neu user chua yeu cau.
- Khong tach case/PCB/plate rieng.
- Khong lam keycap/stab compatibility qua chi tiet.
- Neu them field moi vao ERD thi phai cap nhat:
  - FHD neu anh huong chuc nang.
  - Use cases neu anh huong luong nguoi dung.
  - DFD neu anh huong luong du lieu.
  - Class diagram neu anh huong code.
  - Seed dataset neu field not null hoac can test.
- Neu sua status enum thi phai sua validation, seed va query check.

## 17. Viec can lam tiep

Nhung viec nen lam tiep theo thu tu:

1. Tao file SQL schema/migration tu ERD DBML moi.
2. Tao file SQL check dataset rieng, dua cac query trong muc 14 vao.
3. Chay schema tren database test refactor.
4. Chay seed dataset.
5. Chay SQL check va sua neu co loi.
6. Doi chieu lai class diagram voi code hien tai.
7. Bat dau refactor entity/model truoc.
8. Refactor service/repository theo ERD moi.
9. Refactor UI theo FHD/use cases.
10. Chay test voi dataset refactor.

## 18. Ket luan hien tai

Du an da chuyen tu mo hinh custom keyboard qua chi tiet sang mo hinh realistic kit-shop gon hon.

Trang thai hien tai:

- ERD refactor da co va giu duoi 25 lien ket.
- FHD moi da rut gon den x.x.
- Use cases da gom theo 3 role.
- DFD da chia thanh context/level 0, level 1, level 2.
- Class diagram da phuc vu viec code refactor.
- Dataset seed da tao cho 17/17 bang.
- Dataset da duoc check cheo voi ERD moi.

Huong tiep theo la tao SQL schema/migration tu ERD moi, chay tren database test, seed dataset, check logic, sau do moi refactor code.
