# Custom Keyboard Builder - DFD Level 1 Refactor

Tai lieu nay phan ra cac tien trinh Level 0 thanh cac tien trinh con theo xu ly du lieu. DFD Level 1 van giu muc nghiep vu, khong liet ke tung nut bam UI va khong dua chi tiet database column vao so do.

## DFD Level 1 - 1.0 Quan Ly Tai Khoan

```mermaid
flowchart LR
    User["Buyer/Seller/Admin"]

    P11(("1.1\nTiep nhan thong tin tai khoan"))
    P12(("1.2\nXac thuc va phan quyen"))
    P13(("1.3\nTra cuu tai khoan"))

    D1[("D1\nNguoi dung va vai tro")]

    User -- "(1) Dang ky/dang nhap" --> P11
    P11 -- "(2) Du lieu tai khoan da chuan hoa" --> P12
    P12 -- "(3) Tao/kiem tra tai khoan" --> D1
    D1 -- "(4) User, role, active status" --> P12
    P12 -- "(5) Ket qua xac thuc va role" --> User

    User -- "(6) Yeu cau xem tai khoan/dang xuat" --> P13
    P13 -- "(7) Yeu cau lay user" --> D1
    D1 -- "(8) Thong tin tai khoan" --> P13
    P13 -- "(9) Thong tin tai khoan/ket qua dang xuat" --> User
```

## DFD Level 1 - 2.0 Quan Ly Build Keyboard

```mermaid
flowchart LR
    Buyer["Buyer"]

    P21(("2.1\nTra cuu catalog"))
    P22(("2.2\nXu ly cau hinh build"))
    P23(("2.3\nTinh tong gia snapshot"))
    P24(("2.4\nLuu va tra cuu build"))

    D3[("D3\nCatalog keyboard")]
    D4[("D4\nBuild keyboard")]

    Buyer -- "(1) Yeu cau catalog/tao build" --> P21
    P21 -- "(2) Yeu cau catalog available" --> D3
    D3 -- "(3) Kit, switch, keycap, stab, accessory" --> P21
    P21 -- "(4) Catalog de buyer chon" --> Buyer

    Buyer -- "(5) Kit va build items duoc chon" --> P22
    P22 -- "(6) Yeu cau thong tin catalog" --> D3
    D3 -- "(7) Thong tin kit/item/price/available" --> P22
    P22 -- "(8) Cau hinh hop le" --> P23
    P22 -- "(9) Canh bao/loi tuong thich" --> Buyer

    P23 -- "(10) Tong gia tam tinh" --> Buyer
    P23 -- "(11) Build can luu" --> P24
    Buyer -- "(12) Yeu cau luu/xem/luu tru build" --> P24
    P24 -- "(13) Luu/lay/cap nhat build" --> D4
    D4 -- "(14) Build da luu/danh sach build" --> P24
    P24 -- "(15) Ket qua build" --> Buyer
```

## DFD Level 1 - 3.0 Quan Ly Request Build

```mermaid
flowchart LR
    Buyer["Buyer"]
    Seller["Seller"]

    P31(("3.1\nTiep nhan request moi"))
    P32(("3.2\nTao request build"))
    P33(("3.3\nTra cuu request"))
    P34(("3.4\nCap nhat trang thai request"))

    D1[("D1\nNguoi dung va vai tro")]
    D2[("D2\nHo so seller")]
    D4[("D4\nBuild keyboard")]
    D5[("D5\nRequest build")]

    Buyer -- "(1) Build id, seller id, note" --> P31
    P31 -- "(2) Yeu cau kiem tra seller user" --> D1
    D1 -- "(3) Seller active/role" --> P31
    P31 -- "(4) Yeu cau seller profile" --> D2
    D2 -- "(5) Seller verified" --> P31
    P31 -- "(6) Request hop le" --> P32
    P32 -- "(7) Lay build snapshot" --> D4
    D4 -- "(8) Build cua buyer va total snapshot" --> P32
    P32 -- "(9) Request moi Pending" --> D5
    D5 -- "(10) Request da tao" --> P32
    P32 -- "(11) Ket qua gui request" --> Buyer

    Buyer -- "(12) Yeu cau xem request" --> P33
    Seller -- "(13) Yeu cau xem request duoc gan" --> P33
    P33 -- "(14) Lay request" --> D5
    D5 -- "(15) Request/status/payload" --> P33
    P33 -- "(16) Request va status" --> Buyer
    P33 -- "(17) Request va chi tiet build" --> Seller

    Seller -- "(18) Status moi" --> P34
    P34 -- "(19) Lay request hien tai" --> D5
    D5 -- "(20) Request hien tai" --> P34
    P34 -- "(21) Request da cap nhat" --> D5
    P34 -- "(22) Ket qua cap nhat" --> Seller
    P34 -- "(23) Status moi" --> Buyer
```

## DFD Level 1 - 4.0 Quan Tri He Thong

```mermaid
flowchart LR
    Admin["Admin"]

    P41(("4.1\nQuan ly user"))
    P42(("4.2\nQuan ly seller profile"))
    P43(("4.3\nQuan ly catalog"))
    P44(("4.4\nTra cuu audit log"))

    D1[("D1\nNguoi dung va vai tro")]
    D2[("D2\nHo so seller")]
    D3[("D3\nCatalog keyboard")]
    D6[("D6\nAudit log")]

    Admin -- "(1) Yeu cau user/role" --> P41
    P41 -- "(2) Lay/cap nhat user" --> D1
    D1 -- "(3) User/role hien tai" --> P41
    P41 -- "(4) Log user action" --> D6
    P41 -- "(5) Ket qua user" --> Admin

    Admin -- "(6) Yeu cau seller profile" --> P42
    P42 -- "(7) Lay/cap nhat seller profile" --> D2
    D2 -- "(8) Seller profile" --> P42
    P42 -- "(9) Kiem tra user seller" --> D1
    D1 -- "(10) User seller" --> P42
    P42 -- "(11) Log seller action" --> D6
    P42 -- "(12) Ket qua seller" --> Admin

    Admin -- "(13) Yeu cau catalog" --> P43
    P43 -- "(14) Lay/cap nhat catalog" --> D3
    D3 -- "(15) Catalog hien tai" --> P43
    P43 -- "(16) Log catalog action" --> D6
    P43 -- "(17) Ket qua catalog" --> Admin

    Admin -- "(18) Yeu cau audit log" --> P44
    P44 -- "(19) Lay audit log" --> D6
    D6 -- "(20) Audit log" --> P44
    P44 -- "(21) Audit log hien thi" --> Admin
```

## DFD Level 1 - 5.0 Quan Ly Chat

```mermaid
flowchart LR
    Buyer["Buyer"]
    Seller["Seller"]
    Admin["Admin"]

    P51(("5.1\nMo hoac tao hoi thoai"))
    P52(("5.2\nKiem tra quyen chat"))
    P53(("5.3\nLuu tin nhan"))
    P54(("5.4\nTra cuu lich su chat"))

    D1[("D1\nNguoi dung va vai tro")]
    D5[("D5\nRequest build")]
    D7[("D7\nChat")]

    Buyer -- "(1) Mo chat voi seller" --> P51
    Seller -- "(2) Mo chat voi buyer/admin" --> P51
    Admin -- "(3) Mo chat voi seller" --> P51
    P51 -- "(4) Participant va request neu co" --> P52
    D1 -- "(5) User/role/active status" --> P52
    D5 -- "(6) Request lien quan" --> P52
    P52 -- "(7) Hoi thoai hop le" --> D7
    D7 -- "(8) Hoi thoai hien co" --> P51

    Buyer -- "(9) Tin nhan buyer" --> P53
    Seller -- "(10) Tin nhan seller" --> P53
    Admin -- "(11) Tin nhan admin" --> P53
    P53 -- "(12) Tin nhan da luu" --> D7
    D7 -- "(13) Lich su tin nhan" --> P54
    P54 -- "(14) Lich su chat" --> Buyer
    P54 -- "(15) Lich su chat" --> Seller
    P54 -- "(16) Lich su chat" --> Admin
```

## Chu Thich Can Bang Level 1

| Tien trinh Level 0 | Tien trinh con Level 1 |
| --- | --- |
| 1.0 Quan ly tai khoan | 1.1 Tiep nhan thong tin tai khoan; 1.2 Xac thuc va phan quyen; 1.3 Tra cuu tai khoan |
| 2.0 Quan ly build keyboard | 2.1 Tra cuu catalog; 2.2 Xu ly cau hinh build; 2.3 Tinh tong gia snapshot; 2.4 Luu va tra cuu build |
| 3.0 Quan ly request build | 3.1 Tiep nhan request moi; 3.2 Tao request build; 3.3 Tra cuu request; 3.4 Cap nhat trang thai request |
| 4.0 Quan tri he thong | 4.1 Quan ly user; 4.2 Quan ly seller profile; 4.3 Quan ly catalog; 4.4 Tra cuu audit log |
| 5.0 Quan ly chat | 5.1 Mo hoac tao hoi thoai; 5.2 Kiem tra quyen chat; 5.3 Luu tin nhan; 5.4 Tra cuu lich su chat |

## Ranh Gioi

- Level 1 khong tach tung thao tac UI nhu bam luu, bam gui request hay filter catalog.
- D3 khong co seller inventory va khong co case/PCB/plate rieng le.
- Chat chi co Buyer-Seller va Seller-Admin/Admin-Seller.

