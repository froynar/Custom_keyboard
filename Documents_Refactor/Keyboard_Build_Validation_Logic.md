# Keyboard build validation logic

## Muc tieu refactor

DBML trong `Documents_Refactor` dang chuyen he thong tu mo hinh chon tung linh kien roi nhu `case`, `pcb`, `plate` sang mo hinh thuc te hon: buyer chon mot `keyboard_kits`, sau do them switch, keycap, stabilizer va accessory/mod neu can.

Huong nay hop ly voi shop linh kien ban phim vi kit thuong da gom case, PCB, plate, foam, cable hoac daughterboard. Logic kiem tra build vi vay nen xoay quanh kit truoc, roi moi kiem tra cac mon add-on co phu hop voi kit hay khong.

## Diem nen bo sung hoac thay doi trong DBML

1. Dung `decimal` thay vi `float` cho tat ca gia tien.
   - `price_usd`, `unit_price_snapshot`, `total_cost_snapshot` nen la decimal/number co scale co dinh.
   - Gia tien khong nen dung floating point vi de sai lam tron.

2. Toi gian layout.
   - Layout chi can `layout_id`, `layout_name`, `form_factor`, `key_count`.
   - So switch can mua lay tu `keyboard_kits.required_switch_quantity`.
   - Khong can tach ANSI/ISO, spacebar, bottom row trong ERD giai doan refactor dau.

3. Toi gian compatibility.
   - `keycap_sets.supported_form_factor` la du cho giai doan nay.
   - `stabilizers.supported_layouts` dai dien cho mot goi stab co ban phu hop layout.
   - Khong kiem ANSI/ISO, 6.25u/7u, enter, shift trong database.

4. Toi gian stabilizer.
   - Stabilizer duoc xem la mot goi phu hop layout/form factor.
   - Chi can check `stabilizers.supported_layouts` co phu hop voi layout/kit hay khong.
   - Khong luu kich thuoc stab rieng le.

5. Toi gian included parts.
   - Dung `keyboard_kits.included_parts` dang text de hien thi.
   - Khong parser JSON trong giai doan dau.
   - Build van yeu cau add-on thong qua `build_items` neu buyer chon them.

6. Noi truc tiep `build_items` den linh kien.
   - `build_items` dung cac FK nullable: `switch_id`, `keycap_id`, `stab_id`, `accessory_id`.
   - Moi dong build item chi duoc set dung mot FK san pham.
   - Kit khong nam trong `build_items`; kit da nam o `builds.kit_id`.
   - So luong switch lay tu dong build item co `switch_id`, khong luu rieng `builds.switch_quantity`.

7. Bo `seller_inventory` trong giai doan refactor dau.
   - Code hien tai khong co repository/service/UI cho ton kho.
   - Seller trong du an dang la nguoi nhan request build, chua phai shop co quan ly stock rieng.
   - Bo inventory giup request flow gon hon: buyer gui build hop le den seller verified.
   - Neu sau nay can marketplace that, co the them inventory thanh phase rieng.

8. Them ten hien thi cho san pham neu schema hien tai chua co.
   - DBML da co `kit_name`, `switch_name`, `keycap_name`, `stab_name`.
   - Schema hien tai co nhieu bang linh kien nhung thieu name ro rang, UI/Admin se kho doc.

9. Them rang buoc status.
   - `builds.status`: Draft, Saved, Requested, Archived.
   - `build_requests.status`: Pending, Accepted, In_progress, Completed, Cancelled.
   - Nen co check constraint va logic chuyen trang thai hop le.

10. Toi gian seller profile.
    - ERD refactor chi giu `is_verified` va `verified_at`.
    - Cac field `assigned_by_admin_id`, `verified_by_admin_id`, `assigned_at` co the them sau neu admin audit can chi tiet hon.

11. Khong them timestamp dai tra trong giai doan dau.
    - Chi giu timestamp o build/request/chat neu can workflow.
    - Catalog co the them timestamp sau khi co nhu cau audit san pham.

12. Chat constraints can ro hon.
    - `chat_conversations`: exactly one of `buyer_id` hoac `admin_user_id` duoc set.
    - `sender_user_id` trong `chat_messages` phai la participant hop le cua conversation.
    - Dieu nay DBML co note, nhung implementation nen co check/trigger/service validation.

13. Toi gian accessory/mod.
    - Accessory chi can `accessory_type`, `accessory_name`, `target_component`, gia va trang thai.
    - Khong can `brand_id` cho accessory trong phase dau vi khong co relation brand va khong anh huong validation.
    - Mod chi can `mod_type`, `target_component`, `notes`.
    - Khong luu spring weight, film flag, lube type rieng trong ERD.

## Logic kiem tra tinh hoan thien cua build

### 1. Kiem tra thong tin co ban

- Buyer ton tai, active, co role Buyer.
- Ten build khong rong sau khi trim.
- Build status hop le voi thao tac hien tai.
- Notes gioi han do dai.
- Khong cho save/request build neu buyer bi khoa.

### 2. Kiem tra kit

- `kit_id` bat buoc khi theo model refactor.
- Kit ton tai va `is_available = true`.
- Brand cua kit ton tai.
- Layout cua kit ton tai.
- `pcb_technology`, `switch_mount` cua kit khong rong.
- Gia kit khong am.
- Build co the bo sung switch/keycap/stabilizer/accessory qua `build_items`.
- Build item phai set dung mot trong cac FK san pham.

### 3. Kiem tra switch

- Switch ton tai va dang available.
- `switch.switch_technology` phai khop `kit.pcb_technology`.
- `switch.mount_type` phai khop `kit.switch_mount`.
- Tong quantity cua build item switch phai lon hon hoac bang `keyboard_kits.required_switch_quantity`.

### 4. Kiem tra keycap

- Keycap ton tai va dang available.
- Keycap phai support layout/form factor cua kit.
- Khong kiem chi tiet ANSI/ISO, spacebar, shift, enter.
- Profile/material chi de hien thi va loc san pham.

### 5. Kiem tra stabilizer

- Stabilizer ton tai va dang available.
- Stabilizer package phai support layout/form factor cua kit.
- Khong kiem kich thuoc tung stab rieng le.

### 6. Kiem tra accessory va mod

- Moi build item co `quantity > 0`.
- Moi build item chi duoc set dung mot FK san pham.
- Keycap va stabilizer thuong co `quantity = 1`; switch/accessory co the > 1.
- Accessory ton tai va dang available.
- `accessory.target_component` phai la Switch, Stabilizer, Kit hoac General.
- Mod target component phai la Switch, Stabilizer, Kit hoac Build.
- Khong kiem chi tiet spring weight, film hay lube type.

### 7. Kiem tra seller nhan request

- Seller ton tai, active, role Seller, da verified.
- Seller khong duoc trung buyer.
- Seller co quyen xem/cap nhat request.
- Khong kiem ton kho trong giai doan refactor dau.

### 8. Kiem tra gia va snapshot

- Tong tien = kit price + sum(build_items.quantity * unit price).
- Gia lay tu catalog tai thoi diem save/request.
- Snapshot price luu vao request/build item tai thoi diem tao request.
- `total_cost_snapshot` phai khop ket qua tinh lai tai thoi diem save/request.
- Lam tron tien theo decimal 2 chu so.

### 9. Kiem tra request flow

- Chi build valid moi duoc tao request.
- Build request lay buyer thong qua `build_id -> builds.buyer_id`, khong luu lap `buyer_id`.
- Seller khong duoc trung buyer.
- Nen chan nhieu request active cho cung mot build neu business flow chi cho phep mot seller xu ly tai mot thoi diem.
- Transition hop le:
  - Pending -> Accepted/Cancelled.
  - Accepted -> In_progress/Cancelled.
  - In_progress -> Completed/Cancelled.
  - Completed/Cancelled la terminal.
- `accepted_at`, `completed_at`, `updated_at` phai cap nhat dung transition.

### 10. Kiem tra chat/request phu tro

- Buyer chi chat voi seller trong conversation buyer-seller.
- Admin chi chat voi seller trong conversation admin-seller.
- Buyer-admin direct chat khong hop le theo DBML note.
- Neu conversation gan voi request, seller trong conversation phai la seller cua request.
- Sender message phai la participant cua conversation.

## Phan loai loi/warning de UI dung duoc

- Error: build khong the save/request, vi thieu mon bat buoc hoac khong tuong thich.
- Warning: build van co the save, nhung can luu y thuc te, vi du khong co switch spare hoac profile keycap co the khong toi uu.
- Info: thong tin tinh toan, vi du tong tien, so switch can mua, cac mon kit da include.

## Thu tu validation de implement

1. Normalize input.
2. Validate buyer/build metadata.
3. Load kit va layout.
4. Validate build items co dung mot FK san pham moi dong.
5. Validate switch/keycap/stabilizer compatibility.
6. Validate accessory/mod rules.
7. Validate seller neu tao request.
8. Calculate price snapshots.
9. Validate status/request transition.
10. Tra ve `BuildValidationResult` gom `errors`, `warnings`, `infos`, `total_cost`, `required_switch_quantity`.
