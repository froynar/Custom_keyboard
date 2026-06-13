# Custom Keyboard Builder - DFD Level 2 Refactor

Tai lieu nay chi phan ra cac tien trinh Level 1 co logic du lieu phuc tap:

- `2.2 Xu ly cau hinh build`
- `3.4 Cap nhat trang thai request`
- `5.3 Luu tin nhan`

Nhung tien trinh CRUD quan tri da du ro o Level 1 va Use Cases nen khong bung tiep de tranh trung lap.

## DFD Level 2 - 2.2 Xu Ly Cau Hinh Build

```mermaid
flowchart LR
    Buyer["Buyer"]

    P221(("2.2.1\nNhan lua chon build"))
    P222(("2.2.2\nKiem tra kit"))
    P223(("2.2.3\nKiem tra build items"))
    P224(("2.2.4\nLap cau hinh hop le"))
    P23(("2.3\nTinh tong gia snapshot"))

    D3[("D3\nCatalog keyboard")]

    Buyer -- "(1) Kit, item ids, quantity, mod presets/notes" --> P221
    P221 -- "(2) Kit id" --> P222
    P222 -- "(3) Yeu cau kit/layout/brand" --> D3
    D3 -- "(4) Kit, layout, price, technology, mount" --> P222
    P222 -- "(5) Kit hop le" --> P223
    P221 -- "(6) Switch/keycap/stab/accessory da chon" --> P223
    P223 -- "(7) Yeu cau thong tin items" --> D3
    D3 -- "(8) Item available, price, form factor, technology, mount" --> P223
    P223 -- "(9) Ket qua tuong thich/loi" --> P224
    P224 -- "(10) Cau hinh hop le" --> P23
    P224 -- "(11) Canh bao hoac loi" --> Buyer
```

### Chu Thich Luong Du Lieu

| So | Nguon | Dich | Luong du lieu |
| --- | --- | --- | --- |
| (1) | Buyer | 2.2.1 | Kit duoc chon, switch/keycap/stab/accessory, quantity, mod preset, switch mod quantity va spring weight neu la Spring swap |
| (2) | 2.2.1 | 2.2.2 | Kit id can kiem tra |
| (3) | 2.2.2 | D3 | Yeu cau lay keyboard kit, layout va brand |
| (4) | D3 | 2.2.2 | Kit available, layout/form factor, required switch quantity, technology, mount, price |
| (5) | 2.2.2 | 2.2.3 | Kit hop le lam nen tang kiem tra item |
| (6) | 2.2.1 | 2.2.3 | Danh sach build items da chon |
| (7) | 2.2.3 | D3 | Yeu cau lay switch, keycap, stabilizer va accessory |
| (8) | D3 | 2.2.3 | Available status, price va thuoc tinh can so voi kit |
| (9) | 2.2.3 | 2.2.4 | Ket qua: switch technology/mount khop kit, keycap/stab support layout, accessory target hop le, quantity hop le, Spring swap 30-76g |
| (10) | 2.2.4 | 2.3 | Cau hinh build hop le de tinh gia |
| (11) | 2.2.4 | Buyer | Canh bao hoac loi tuong thich hien tren UI |

## DFD Level 2 - 3.4 Cap Nhat Trang Thai Request

```mermaid
flowchart LR
    Seller["Seller"]
    Buyer["Buyer"]

    P341(("3.4.1\nNhan yeu cau doi status"))
    P342(("3.4.2\nLay request hien tai"))
    P343(("3.4.3\nKiem tra seller va status"))
    P344(("3.4.4\nGhi status moi"))
    P345(("3.4.5\nTra ket qua"))

    D1[("D1\nNguoi dung va vai tro")]
    D5[("D5\nRequest build")]

    Seller -- "(1) Request id va status moi" --> P341
    P341 -- "(2) Request id" --> P342
    P342 -- "(3) Lay request hien tai" --> D5
    D5 -- "(4) Request hien tai" --> P342
    P342 -- "(5) Seller id trong request" --> P343
    P343 -- "(6) Kiem tra user seller" --> D1
    D1 -- "(7) Seller role/active" --> P343
    P343 -- "(8) Status moi hop le" --> P344
    P344 -- "(9) Request da cap nhat" --> D5
    D5 -- "(10) Request sau cap nhat" --> P345
    P345 -- "(11) Ket qua cap nhat" --> Seller
    P345 -- "(12) Trang thai moi" --> Buyer
```

### Rule Trang Thai Request

| Trang thai hien tai | Trang thai co the chuyen | Ghi chu |
| --- | --- | --- |
| Pending | Accepted, Cancelled | Seller nhan request hoac huy. |
| Accepted | In_progress, Cancelled | Seller bat dau xu ly hoac huy. |
| In_progress | Completed, Cancelled | Seller hoan thanh hoac huy. |
| Completed | Khong chuyen tiep | Trang thai ket thuc. |
| Cancelled | Khong chuyen tiep | Trang thai ket thuc. |

## DFD Level 2 - 5.3 Luu Tin Nhan

```mermaid
flowchart LR
    Sender["Buyer/Seller/Admin"]
    Receiver["Nguoi nhan hop le"]

    P531(("5.3.1\nNhan tin nhan"))
    P532(("5.3.2\nKiem tra conversation"))
    P533(("5.3.3\nLuu tin nhan"))
    P534(("5.3.4\nTra tin nhan moi"))

    D1[("D1\nNguoi dung va vai tro")]
    D7[("D7\nChat")]

    Sender -- "(1) Conversation id va message text" --> P531
    P531 -- "(2) Sender va conversation id" --> P532
    P532 -- "(3) Lay user/role" --> D1
    D1 -- "(4) User/role/active status" --> P532
    P532 -- "(5) Lay conversation" --> D7
    D7 -- "(6) Seller-buyer/admin participants" --> P532
    P532 -- "(7) Tin nhan hop le" --> P533
    P533 -- "(8) Message moi" --> D7
    D7 -- "(9) Message vua luu" --> P534
    P534 -- "(10) Tin nhan moi" --> Sender
    P534 -- "(11) Tin nhan moi" --> Receiver
```

### Chu Thich Chat

| Kiem tra | Noi dung |
| --- | --- |
| Participant | Sender phai la seller, buyer hoac admin cua conversation. |
| Cap chat | Conversation hop le la Buyer-Seller hoac Admin-Seller. |
| Buyer-Admin | Khong cho tao/guid tin nhan truc tiep Buyer-Admin. |
| Noi dung | Message text khong duoc rong. |

## Kiem Tra Can Bang Voi Level 1

| Level 1 | Level 2 |
| --- | --- |
| 2.2 Xu ly cau hinh build | 2.2.1 Nhan lua chon build; 2.2.2 Kiem tra kit; 2.2.3 Kiem tra build items; 2.2.4 Lap cau hinh hop le |
| 3.4 Cap nhat trang thai request | 3.4.1 Nhan yeu cau doi status; 3.4.2 Lay request hien tai; 3.4.3 Kiem tra seller va status; 3.4.4 Ghi status moi; 3.4.5 Tra ket qua |
| 5.3 Luu tin nhan | 5.3.1 Nhan tin nhan; 5.3.2 Kiem tra conversation; 5.3.3 Luu tin nhan; 5.3.4 Tra tin nhan moi |

## Ranh Gioi Level 2

- Level 2 khong tach case, PCB, plate thanh tien trinh rieng; cac phan nay da nam trong keyboard kit.
- Level 2 khong co seller inventory, stock hay seller price.
- Khong dua API, DTO, controller, migration hoac chi tiet bang/cot vao so do.
