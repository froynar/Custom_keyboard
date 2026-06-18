# Custom Keyboard Builder - DFD Level 1 Simplified

Tai lieu nay la ban rut gon cua `Custom_Keyboard_DFD_Level1_Refactor.md`.
Muc tieu la giu dung can bang voi DFD Level 0 rut gon, nhung moi so do Level 1 chi the hien cac xu ly va kho du lieu chinh.

## Quy Uoc Kho Du Lieu Rut Gon

| Ma | Kho du lieu logic | Tuong ung du lieu chinh |
| --- | --- | --- |
| D1 | Nguoi dung & ho so seller | Users, roles, seller profiles, active/verified status. |
| D2 | Catalog keyboard | Brands, layouts, keyboard kits, switches, keycaps, stabilizers, accessories. |
| D3 | Build & request | Builds, build items, build mods, build requests va request snapshot. |
| D4 | Chat | Chat conversations va chat messages. |
| D5 | Audit & don seller | Audit logs va seller applications. |
| D6 | Device & QC results | Devices, device test sessions va per-key QC results. |

## DFD Level 1 - 1.0 Quan Ly Tai Khoan

```mermaid
flowchart LR
    User["Buyer/Seller/Admin"]

    P11(("1.1\nTiep nhan thong tin"))
    P12(("1.2\nXac thuc va phan quyen"))
    P13(("1.3\nTra cuu profile/dang xuat"))

    D1[("D1\nNguoi dung\n& ho so seller")]

    User -- "(1) Dang ky/dang nhap" --> P11
    P11 -- "(2) Du lieu tai khoan" --> P12
    P12 <--> D1
    P12 -- "(3) Ket qua xac thuc va role" --> User

    User -- "(4) Xem profile/dang xuat" --> P13
    P13 <--> D1
    P13 -- "(5) Profile hoac ket qua dang xuat" --> User
```

### Chu Thich

| So | Luong du lieu |
| --- | --- |
| (1) | Buyer dang ky hoac Buyer/Seller/Admin dang nhap. |
| (2) | Username/email/password/phone da chuan hoa. |
| (3) | Ket qua login, role, active status. |
| (4) | Yeu cau xem profile hoac ket thuc phien. |
| (5) | Thong tin profile cua chinh nguoi dung hoac ket qua dang xuat. |

## DFD Level 1 - 2.0 Quan Ly Build Keyboard

```mermaid
flowchart LR
    Buyer["Buyer"]

    P21(("2.1\nTra cuu catalog"))
    P22(("2.2\nXu ly cau hinh build"))
    P23(("2.3\nTinh tong gia"))
    P24(("2.4\nLuu/tra cuu build"))

    D2[("D2\nCatalog\nkeyboard")]
    D3[("D3\nBuild\n& request")]

    Buyer -- "(1) Yeu cau catalog/tao build" --> P21
    P21 <--> D2
    P21 -- "(2) Catalog kha dung" --> Buyer

    Buyer -- "(3) Kit, item, mod, note" --> P22
    P22 <--> D2
    P22 -- "(4) Loi/canh bao neu co" --> Buyer
    P22 -- "(5) Cau hinh hop le" --> P23

    P23 -- "(6) Tong gia snapshot" --> Buyer
    P23 -- "(7) Build can luu" --> P24
    Buyer -- "(8) Luu/xem/luu tru build" --> P24
    P24 <--> D3
    P24 -- "(9) Ket qua build" --> Buyer
```

### Chu Thich

| So | Luong du lieu |
| --- | --- |
| (1) | Buyer mo catalog hoac bat dau build moi. |
| (2) | Kit, switch, keycap, stabilizer, accessory available va thong tin gia/tuong thich. |
| (3) | Kit duoc chon, item quantity, mod preset, switch mod quantity, spring weight va build notes. |
| (4) | Loi/canh bao: item khong hop kit, thieu switch, mod switch quantity khong hop le. |
| (5) | Cau hinh du dieu kien tinh gia. |
| (6) | Tong gia tam tinh theo catalog va quantity. |
| (7) | Build snapshot can luu. |
| (8) | Yeu cau luu build, xem build hoac archive build. |
| (9) | Build da luu, danh sach build hoac trang thai archive. |

## DFD Level 1 - 3.0 Quan Ly Request Build

```mermaid
flowchart LR
    Buyer["Buyer"]
    Seller["Seller"]

    P31(("3.1\nTiep nhan request"))
    P32(("3.2\nTao request"))
    P33(("3.3\nTra cuu request"))
    P34(("3.4\nCap nhat trang thai"))

    D1[("D1\nNguoi dung\n& ho so seller")]
    D3[("D3\nBuild\n& request")]

    Buyer -- "(1) Build va seller duoc chon" --> P31
    P31 <--> D1
    P31 -- "(2) Seller hop le" --> P32
    P32 <--> D3
    P32 -- "(3) Ket qua gui request" --> Buyer

    Buyer -- "(4) Xem request da gui" --> P33
    Seller -- "(5) Xem request duoc gan" --> P33
    P33 <--> D3
    P33 -- "(6) Request/status" --> Buyer
    P33 -- "(7) Request va build snapshot" --> Seller

    Seller -- "(8) Status moi" --> P34
    P34 <--> D1
    P34 <--> D3
    P34 -- "(9) Ket qua cap nhat" --> Seller
    P34 -- "(10) Trang thai moi" --> Buyer
```

### Chu Thich

| So | Luong du lieu |
| --- | --- |
| (1) | Build id, seller id va request note. |
| (2) | Seller active, role Seller va profile verified. |
| (3) | Request moi trang thai Pending hoac loi neu khong hop le. |
| (4) | Buyer xem tien do request da gui. |
| (5) | Seller xem request duoc gan cho minh. |
| (6) | Trang thai request cho Buyer. |
| (7) | Snapshot build va thong tin request cho Seller. |
| (8) | Accepted, In_progress, Completed hoac Cancelled theo state machine. |
| (9) | Ket qua xu ly cho Seller. |
| (10) | Trang thai request moi cho Buyer. |

## DFD Level 1 - 4.0 Quan Tri He Thong

```mermaid
flowchart LR
    Admin["Admin"]
    Buyer["Buyer"]

    P41(("4.1\nDashboard admin"))
    P42(("4.2\nQuan ly user"))
    P43(("4.3\nQuan ly seller profile"))
    P44(("4.4\nQuan ly catalog"))
    P45(("4.5\nXem audit log"))
    P46(("4.6\nDuyet don seller"))

    D1[("D1\nNguoi dung\n& ho so seller")]
    D2[("D2\nCatalog\nkeyboard")]
    D3[("D3\nBuild\n& request")]
    D5[("D5\nAudit\n& don seller")]

    Admin -- "(1) Mo dashboard" --> P41
    P41 <--> D1
    P41 <--> D2
    P41 <--> D3
    P41 -- "(2) Tong quan quan tri" --> Admin

    Admin -- "(3) User/role/active" --> P42
    P42 <--> D1
    P42 --> D5
    P42 -- "(4) Ket qua user" --> Admin

    Admin -- "(5) Seller profile/verified" --> P43
    P43 <--> D1
    P43 --> D5
    P43 -- "(6) Ket qua seller" --> Admin

    Admin -- "(7) Catalog data" --> P44
    P44 <--> D2
    P44 --> D5
    P44 -- "(8) Ket qua catalog" --> Admin

    Admin -- "(9) Yeu cau audit" --> P45
    P45 <--> D5
    P45 -- "(10) Audit log" --> Admin

    Buyer -- "(11) Nop don seller" --> P46
    Admin -- "(12) Duyet/tu choi don" --> P46
    P46 <--> D1
    P46 <--> D5
    P46 -- "(13) Trang thai don" --> Buyer
    P46 -- "(14) Ket qua duyet" --> Admin
```

### Chu Thich

| So | Luong du lieu |
| --- | --- |
| (1) | Admin xem tong quan user, seller, catalog va request. |
| (2) | Dashboard admin. |
| (3) | Yeu cau xem/khoa/mo/cap nhat role user. |
| (4) | Ket qua thao tac user va audit neu can. |
| (5) | Yeu cau tao/cap nhat/verify/unverify seller profile. |
| (6) | Ket qua thao tac seller profile va audit neu can. |
| (7) | Yeu cau them/sua/an/hien catalog. |
| (8) | Ket qua thao tac catalog va audit neu can. |
| (9) | Bo loc/yeu cau xem audit log. |
| (10) | Lich su thao tac quan trong. |
| (11) | Don seller cua Buyer: shop name, phone, address, note. |
| (12) | Admin chap nhan hoac tu choi don. |
| (13) | Pending, Approved hoac Rejected. |
| (14) | Neu approve: role Seller, seller profile verified va audit duoc cap nhat. |

## DFD Level 1 - 5.0 Quan Ly Chat

```mermaid
flowchart LR
    Buyer["Buyer"]
    Seller["Seller"]
    Admin["Admin"]

    P51(("5.1\nMo/tao hoi thoai"))
    P52(("5.2\nKiem tra quyen chat"))
    P53(("5.3\nLuu tin nhan"))
    P54(("5.4\nTra cuu lich su"))

    D1[("D1\nNguoi dung\n& ho so seller")]
    D3[("D3\nBuild\n& request")]
    D4[("D4\nChat")]

    Buyer -- "(1) Mo chat voi seller" --> P51
    Seller -- "(2) Mo chat voi buyer/admin" --> P51
    Admin -- "(3) Mo chat voi seller" --> P51
    P51 --> P52
    P52 <--> D1
    P52 <--> D3
    P52 -- "(4) Hoi thoai hop le" --> P53
    P52 -- "(5) Hoi thoai hop le" --> P54

    Buyer -- "(6) Tin nhan buyer" --> P53
    Seller -- "(7) Tin nhan seller" --> P53
    Admin -- "(8) Tin nhan admin" --> P53
    P53 <--> D4
    P54 <--> D4
    P54 -- "(9) Lich su chat" --> Buyer
    P54 -- "(9) Lich su chat" --> Seller
    P54 -- "(9) Lich su chat" --> Admin
```

### Chu Thich

| So | Luong du lieu |
| --- | --- |
| (1) | Buyer chi mo chat voi seller hop le. |
| (2) | Seller mo chat voi buyer lien quan request hoac admin. |
| (3) | Admin mo chat voi seller. |
| (4) | Conversation Buyer-Seller hoac Seller-Admin/Admin-Seller da duoc validate. |
| (5) | Yeu cau lay lich su conversation hop le. |
| (6) | Tin nhan Buyer gui Seller. |
| (7) | Tin nhan Seller gui Buyer hoac Admin. |
| (8) | Tin nhan Admin gui Seller. |
| (9) | Lich su chat va tin nhan moi. |

## DFD Level 1 - 6.0 Device/QC Kiem Tra Keyboard

```mermaid
flowchart LR
    Seller["Seller"]
    Buyer["Buyer"]

    P61(("6.1\nTao/lay tram QC"))
    P62(("6.2\nBat dau phien QC"))
    P63(("6.3\nXu ly telemetry tung phim"))
    P64(("6.4\nTong hop session"))
    P65(("6.5\nTra ket qua QC"))

    D3[("D3\nBuild\n& request")]
    D6[("D6\nDevice\n& QC results")]

    Seller -- "(1) Yeu cau chay QC" --> P61
    P61 <--> D6
    P61 -- "(2) Device active" --> P62
    P62 <--> D3
    P62 <--> D6
    P62 -- "(3) Session Running" --> P63
    P63 <--> D6
    P63 -- "(4) Du ket qua tung phim" --> P64
    P64 <--> D6
    P64 -- "(5) Summary" --> P65
    P65 -- "(6) Bang ket qua tung phim" --> Seller
    P65 -- "(7) Tom tat QC" --> Buyer
```

### Chu Thich

| So | Luong du lieu |
| --- | --- |
| (1) | Seller chay QC cho request dang In_progress. |
| (2) | QC_STATION cua seller duoc tao hoac lay lai. |
| (3) | Session Running gan request, seller, device, switch technology, noise requirement va total keys. |
| (4) | Telemetry tung phim da duoc luu thanh key result. |
| (5) | Tested/pass/warning/fail, average/max latency va noise. |
| (6) | Seller xem ket qua tung key va failure type. |
| (7) | Buyer xem QC summary moi nhat theo request. |

## Kiem Tra Can Bang Voi Level 0 Rut Gon

| Level 0 | Level 1 rut gon |
| --- | --- |
| 1.0 Quan ly tai khoan | 1.1 Tiep nhan thong tin; 1.2 Xac thuc va phan quyen; 1.3 Tra cuu profile/dang xuat |
| 2.0 Quan ly build | 2.1 Tra cuu catalog; 2.2 Xu ly cau hinh build; 2.3 Tinh tong gia; 2.4 Luu/tra cuu build |
| 3.0 Quan ly request | 3.1 Tiep nhan request; 3.2 Tao request; 3.3 Tra cuu request; 3.4 Cap nhat trang thai |
| 4.0 Quan tri he thong | 4.1 Dashboard admin; 4.2 Quan ly user; 4.3 Quan ly seller profile; 4.4 Quan ly catalog; 4.5 Xem audit log; 4.6 Duyet don seller |
| 5.0 Quan ly chat | 5.1 Mo/tao hoi thoai; 5.2 Kiem tra quyen chat; 5.3 Luu tin nhan; 5.4 Tra cuu lich su |
| 6.0 Device/QC | 6.1 Tao/lay tram QC; 6.2 Bat dau phien QC; 6.3 Xu ly telemetry tung phim; 6.4 Tong hop session; 6.5 Tra ket qua QC |

## Ranh Gioi

- Ban rut gon khong thay the file Level 1 chi tiet.
- Khong co chat truc tiep Buyer-Admin.
- Khong co seller inventory, compatibility rules, case/PCB/plate rieng le.
- Device/QC la simulator va luu DB-first; MQTT chi la transport, fallback in-process khong lam mat ket qua.
