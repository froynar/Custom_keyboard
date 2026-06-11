# Custom Keyboard Builder - DFD Duoi Muc Dinh Level 1

Tai lieu nay phan ra cac tien trinh chinh trong DFD Level 0 thanh cac tien trinh con theo xu ly du lieu. Khac voi FHD, Level 1 khong liet ke tung thao tac UI rieng le. Cac man hinh nhu dashboard, danh sach build, danh sach request chi la cach hien thi ket qua cua cac luong tra cuu du lieu.

Quy uoc:

- Moi mui ten la mot luong du lieu mot chieu.
- Khong dung mui ten "phan ra" trong so do DFD.
- Ten tien trinh la dong tu xu ly du lieu, khong phai ten nut bam hay ten man hinh.
- Cac kho du lieu giu cung ma voi DFD Level 0.

## DFD Level 1 - 1.0 Quan Ly Tai Khoan

```mermaid
flowchart LR
    User["Nguoi dung"]

    P11(("1.1\nTiep nhan thong tin tai khoan"))
    P12(("1.2\nXac thuc va phan quyen"))
    P13(("1.3\nTra cuu thong tin tai khoan"))

    D1[("D1\nNguoi dung va vai tro")]

    User -- "(1) Thong tin dang ky/dang nhap" --> P11
    P11 -- "(2) Du lieu tai khoan da chuan hoa" --> P12
    P12 -- "(3) Yeu cau tao/kiem tra tai khoan" --> D1
    D1 -- "(4) User, role, trang thai active" --> P12
    P12 -- "(5) Ket qua xac thuc va role" --> User

    User -- "(6) Yeu cau xem tai khoan/dang xuat" --> P13
    P13 -- "(7) Yeu cau lay thong tin user" --> D1
    D1 -- "(8) Thong tin tai khoan" --> P13
    P13 -- "(9) Thong tin tai khoan/ket qua dang xuat" --> User
```

### Chu Thich Luong Du Lieu

| So | Nguon | Dich | Luong du lieu |
| --- | --- | --- | --- |
| (1) | Nguoi dung | 1.1 Tiep nhan thong tin tai khoan | Username/email/password/thong tin dang ky hoac dang nhap |
| (2) | 1.1 Tiep nhan thong tin tai khoan | 1.2 Xac thuc va phan quyen | Du lieu tai khoan da chuan hoa |
| (3) | 1.2 Xac thuc va phan quyen | D1 | Yeu cau tao tai khoan moi hoac kiem tra tai khoan |
| (4) | D1 | 1.2 Xac thuc va phan quyen | User, role, password hash, trang thai active |
| (5) | 1.2 Xac thuc va phan quyen | Nguoi dung | Ket qua dang ky/dang nhap va man hinh theo role |
| (6) | Nguoi dung | 1.3 Tra cuu thong tin tai khoan | Yeu cau xem thong tin tai khoan hoac dang xuat |
| (7) | 1.3 Tra cuu thong tin tai khoan | D1 | Yeu cau lay thong tin user |
| (8) | D1 | 1.3 Tra cuu thong tin tai khoan | Ten, email, phone, role, trang thai |
| (9) | 1.3 Tra cuu thong tin tai khoan | Nguoi dung | Thong tin tai khoan hien thi hoac ket qua dang xuat |

## DFD Level 1 - 2.0 Quan Ly Build Keyboard

```mermaid
flowchart LR
    Buyer["Buyer"]

    P21(("2.1\nTra cuu danh muc va rule"))
    P22(("2.2\nXu ly cau hinh build"))
    P23(("2.3\nTinh tong gia snapshot"))
    P24(("2.4\nLuu va tra cuu build"))

    D3[("D3\nDanh muc linh kien va rule")]
    D4[("D4\nBuild keyboard")]

    Buyer -- "(1) Yeu cau cau hinh build" --> P21
    P21 -- "(2) Yeu cau danh muc/rule" --> D3
    D3 -- "(3) Linh kien kha dung va rule tuong thich" --> P21
    P21 -- "(4) Danh muc loc theo dieu kien" --> Buyer

    Buyer -- "(5) Lua chon layout/linh kien/mod" --> P22
    P22 -- "(6) Yeu cau kiem tra tuong thich" --> D3
    D3 -- "(7) Ket qua rule/danh muc hop le" --> P22
    P22 -- "(8) Cau hinh build hop le" --> P23
    P22 -- "(15) Canh bao cau hinh khong hop le" --> Buyer

    P23 -- "(9) Cau hinh kem tong gia" --> Buyer
    Buyer -- "(10) Yeu cau luu/xem build" --> P24
    P23 -- "(11) Build can luu" --> P24
    P24 -- "(12) Luu/lay build" --> D4
    D4 -- "(13) Build da luu/danh sach build" --> P24
    P24 -- "(14) Ket qua luu hoac danh sach build" --> Buyer
```

### Chu Thich Luong Du Lieu

| So | Nguon | Dich | Luong du lieu |
| --- | --- | --- | --- |
| (1) | Buyer | 2.1 Tra cuu danh muc va rule | Yeu cau bat dau cau hinh build hoac lay danh muc linh kien |
| (2) | 2.1 Tra cuu danh muc va rule | D3 | Yeu cau danh sach layout, linh kien, rule tuong thich |
| (3) | D3 | 2.1 Tra cuu danh muc va rule | Linh kien available, layout, brand, compatibility rule |
| (4) | 2.1 Tra cuu danh muc va rule | Buyer | Danh muc linh kien duoc loc de hien thi |
| (5) | Buyer | 2.2 Xu ly cau hinh build | Layout, case, PCB, plate, switch, keycap, stabilizer, mod, ghi chu |
| (6) | 2.2 Xu ly cau hinh build | D3 | Yeu cau kiem tra rule tuong thich va trang thai available |
| (7) | D3 | 2.2 Xu ly cau hinh build | Ket qua rule va linh kien hop le/khong hop le |
| (8) | 2.2 Xu ly cau hinh build | 2.3 Tinh tong gia snapshot | Cau hinh build da duoc kiem tra |
| (9) | 2.3 Tinh tong gia snapshot | Buyer | Tong gia snapshot va cau hinh tam thoi |
| (10) | Buyer | 2.4 Luu va tra cuu build | Yeu cau luu build, xem danh sach build hoac xem build da luu |
| (11) | 2.3 Tinh tong gia snapshot | 2.4 Luu va tra cuu build | Build can luu kem tong gia snapshot |
| (12) | 2.4 Luu va tra cuu build | D4 | Thong tin build can luu hoac yeu cau lay build |
| (13) | D4 | 2.4 Luu va tra cuu build | Build da luu, danh sach build cua buyer |
| (14) | 2.4 Luu va tra cuu build | Buyer | Ket qua luu build, danh sach build hoac chi tiet build |
| (15) | 2.2 Xu ly cau hinh build | Buyer | Canh bao hoac thong bao loi khi cau hinh khong hop le |

## DFD Level 1 - 3.0 Quan Ly Request Build

```mermaid
flowchart LR
    Buyer["Buyer"]
    Seller["Seller"]

    P31(("3.1\nTiep nhan yeu cau gui request"))
    P32(("3.2\nTao request build"))
    P33(("3.3\nTra cuu request"))
    P34(("3.4\nCap nhat trang thai request"))

    D2[("D2\nHo so seller")]
    D4[("D4\nBuild keyboard")]
    D5[("D5\nRequest build")]

    Buyer -- "(1) Build va seller duoc chon" --> P31
    P31 -- "(2) Yeu cau kiem tra seller" --> D2
    D2 -- "(3) Seller verified/active" --> P31
    P31 -- "(4) Ma build can gui" --> P32
    P32 -- "(5) Yeu cau lay build" --> D4
    D4 -- "(6) Build snapshot" --> P32
    P32 -- "(7) Request moi" --> D5
    D5 -- "(8) Request da tao/trang thai Pending" --> P32
    P32 -- "(9) Ket qua gui request" --> Buyer

    Buyer -- "(10) Yeu cau xem request cua minh" --> P33
    Seller -- "(11) Yeu cau xem request duoc gan" --> P33
    P33 -- "(12) Yeu cau lay request" --> D5
    D5 -- "(13) Danh sach/chi tiet request" --> P33
    P33 -- "(14) Request va trang thai" --> Buyer
    P33 -- "(15) Request va chi tiet build snapshot" --> Seller

    Seller -- "(16) Trang thai moi" --> P34
    P34 -- "(17) Yeu cau lay request hien tai" --> D5
    D5 -- "(18) Request hien tai" --> P34
    P34 -- "(19) Request da cap nhat" --> D5
    P34 -- "(20) Ket qua cap nhat" --> Seller
    P34 -- "(21) Trang thai request moi" --> Buyer
```

### Chu Thich Luong Du Lieu

| So | Nguon | Dich | Luong du lieu |
| --- | --- | --- | --- |
| (1) | Buyer | 3.1 Tiep nhan yeu cau gui request | Ma build, seller duoc chon, ghi chu request |
| (2) | 3.1 Tiep nhan yeu cau gui request | D2 | Yeu cau kiem tra seller verified/active |
| (3) | D2 | 3.1 Tiep nhan yeu cau gui request | Thong tin seller kha dung |
| (4) | 3.1 Tiep nhan yeu cau gui request | 3.2 Tao request build | Ma build, buyer, seller, ghi chu |
| (5) | 3.2 Tao request build | D4 | Yeu cau lay build da luu |
| (6) | D4 | 3.2 Tao request build | Build snapshot va tong gia tai thoi diem gui |
| (7) | 3.2 Tao request build | D5 | Request build moi |
| (8) | D5 | 3.2 Tao request build | Request da tao voi trang thai Pending |
| (9) | 3.2 Tao request build | Buyer | Ket qua gui request |
| (10) | Buyer | 3.3 Tra cuu request | Yeu cau xem request/trang thai cua buyer |
| (11) | Seller | 3.3 Tra cuu request | Yeu cau xem request duoc gan cho seller |
| (12) | 3.3 Tra cuu request | D5 | Yeu cau lay danh sach/chi tiet request |
| (13) | D5 | 3.3 Tra cuu request | Request, status, payload snapshot |
| (14) | 3.3 Tra cuu request | Buyer | Danh sach request va trang thai |
| (15) | 3.3 Tra cuu request | Seller | Danh sach request va chi tiet build snapshot |
| (16) | Seller | 3.4 Cap nhat trang thai request | Trang thai moi: Accepted, In_progress, Completed, Cancelled |
| (17) | 3.4 Cap nhat trang thai request | D5 | Yeu cau lay request hien tai |
| (18) | D5 | 3.4 Cap nhat trang thai request | Request hien tai |
| (19) | 3.4 Cap nhat trang thai request | D5 | Request sau khi cap nhat status/time |
| (20) | 3.4 Cap nhat trang thai request | Seller | Ket qua cap nhat request |
| (21) | 3.4 Cap nhat trang thai request | Buyer | Trang thai request moi de buyer theo doi |

## DFD Level 1 - 4.0 Quan Tri He Thong

```mermaid
flowchart LR
    Admin["Admin"]

    P41(("4.1\nQuan ly user va role"))
    P42(("4.2\nQuan ly seller"))
    P43(("4.3\nQuan ly danh muc linh kien"))
    P44(("4.4\nTra cuu audit log"))

    D1[("D1\nNguoi dung va vai tro")]
    D2[("D2\nHo so seller")]
    D3[("D3\nDanh muc linh kien va rule")]
    D6[("D6\nAudit log")]

    Admin -- "(1) Yeu cau quan ly user/role" --> P41
    P41 -- "(2) Lay/cap nhat user" --> D1
    D1 -- "(3) User/role hien tai" --> P41
    P41 -- "(4) Log thao tac user" --> D6
    P41 -- "(5) Ket qua quan ly user" --> Admin

    Admin -- "(6) Yeu cau quan ly seller" --> P42
    P42 -- "(7) Lay/cap nhat seller" --> D2
    D2 -- "(8) Seller profile hien tai" --> P42
    P42 -- "(9) Yeu cau lay user seller" --> D1
    D1 -- "(10) User seller/admin lien quan" --> P42
    P42 -- "(11) Log thao tac seller" --> D6
    P42 -- "(12) Ket qua quan ly seller" --> Admin

    Admin -- "(13) Yeu cau quan ly linh kien" --> P43
    P43 -- "(14) Lay/cap nhat danh muc" --> D3
    D3 -- "(15) Linh kien/rule hien tai" --> P43
    P43 -- "(16) Log thao tac linh kien" --> D6
    P43 -- "(17) Ket qua quan ly linh kien" --> Admin

    Admin -- "(18) Yeu cau xem audit log" --> P44
    P44 -- "(19) Yeu cau lay audit log" --> D6
    D6 -- "(20) Danh sach audit log" --> P44
    P44 -- "(21) Audit log hien thi" --> Admin
```

### Chu Thich Luong Du Lieu

| So | Nguon | Dich | Luong du lieu |
| --- | --- | --- | --- |
| (1) | Admin | 4.1 Quan ly user va role | Yeu cau xem user, ban/mo ban, doi role |
| (2) | 4.1 Quan ly user va role | D1 | Yeu cau lay/cap nhat user, role, is_active |
| (3) | D1 | 4.1 Quan ly user va role | User/role hien tai |
| (4) | 4.1 Quan ly user va role | D6 | Log thao tac user |
| (5) | 4.1 Quan ly user va role | Admin | Ket qua quan ly user |
| (6) | Admin | 4.2 Quan ly seller | Yeu cau xem/cap nhat/verify/unverify seller |
| (7) | 4.2 Quan ly seller | D2 | Yeu cau lay/cap nhat seller profile |
| (8) | D2 | 4.2 Quan ly seller | Seller profile hien tai |
| (9) | 4.2 Quan ly seller | D1 | Yeu cau lay user gan voi seller |
| (10) | D1 | 4.2 Quan ly seller | User seller/admin lien quan |
| (11) | 4.2 Quan ly seller | D6 | Log thao tac seller |
| (12) | 4.2 Quan ly seller | Admin | Ket qua quan ly seller |
| (13) | Admin | 4.3 Quan ly danh muc linh kien | Yeu cau them/sua/an/khoi phuc linh kien hoac rule |
| (14) | 4.3 Quan ly danh muc linh kien | D3 | Yeu cau lay/cap nhat danh muc linh kien |
| (15) | D3 | 4.3 Quan ly danh muc linh kien | Danh muc/rule hien tai |
| (16) | 4.3 Quan ly danh muc linh kien | D6 | Log thao tac linh kien |
| (17) | 4.3 Quan ly danh muc linh kien | Admin | Ket qua quan ly linh kien |
| (18) | Admin | 4.4 Tra cuu audit log | Yeu cau xem audit log |
| (19) | 4.4 Tra cuu audit log | D6 | Yeu cau lay audit log |
| (20) | D6 | 4.4 Tra cuu audit log | Danh sach audit log |
| (21) | 4.4 Tra cuu audit log | Admin | Audit log hien thi |

## DFD Level 1 - 5.0 Quan Ly Chat Realtime

```mermaid
flowchart LR
    Buyer["Buyer"]
    Seller["Seller"]
    Admin["Admin"]

    P51(("5.1\nMo hoac tao hoi thoai"))
    P52(("5.2\nKiem tra quyen chat"))
    P53(("5.3\nLuu tin nhan"))
    P54(("5.4\nTra cuu lich su chat"))
    P55(("5.5\nPhat realtime SignalR"))

    D1[("D1\nNguoi dung va vai tro")]
    D5[("D5\nRequest build")]
    D7[("D7\nChat")]

    Buyer -- "(1) Mo chat voi seller" --> P51
    Seller -- "(2) Mo chat voi buyer/admin" --> P51
    Admin -- "(3) Mo chat voi seller" --> P51
    P51 -- "(4) Yeu cau kiem tra nguoi tham gia" --> P52
    D1 -- "(5) User, role, active status" --> P52
    D5 -- "(6) Request lien quan neu chat theo don" --> P52
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
    P53 -- "(17) Tin nhan moi" --> P55
    P55 -- "(18) SignalR event" --> Buyer
    P55 -- "(19) SignalR event" --> Seller
    P55 -- "(20) SignalR event" --> Admin
```

### Chu Thich Luong Du Lieu

| So | Nguon | Dich | Luong du lieu |
| --- | --- | --- | --- |
| (1) | Buyer | 5.1 Mo hoac tao hoi thoai | Yeu cau mo chat voi seller da chon/request lien quan |
| (2) | Seller | 5.1 Mo hoac tao hoi thoai | Yeu cau mo chat voi buyer hoac admin |
| (3) | Admin | 5.1 Mo hoac tao hoi thoai | Yeu cau mo chat voi seller |
| (4) | 5.1 Mo hoac tao hoi thoai | 5.2 Kiem tra quyen chat | Buyer/seller/admin id va request id neu co |
| (5) | D1 | 5.2 Kiem tra quyen chat | User, role va active status |
| (6) | D5 | 5.2 Kiem tra quyen chat | Request lien quan de xac nhan buyer-seller hop le neu chat theo don |
| (7) | 5.2 Kiem tra quyen chat | D7 | Hoi thoai moi hop le: seller-buyer hoac seller-admin |
| (8) | D7 | 5.1 Mo hoac tao hoi thoai | Hoi thoai hien co |
| (9) | Buyer | 5.3 Luu tin nhan | Noi dung tin nhan buyer gui seller |
| (10) | Seller | 5.3 Luu tin nhan | Noi dung tin nhan seller gui buyer/admin |
| (11) | Admin | 5.3 Luu tin nhan | Noi dung tin nhan admin gui seller |
| (12) | 5.3 Luu tin nhan | D7 | Tin nhan da luu voi sender va sent_at |
| (13) | D7 | 5.4 Tra cuu lich su chat | Danh sach tin nhan theo hoi thoai |
| (14) | 5.4 Tra cuu lich su chat | Buyer | Lich su chat voi seller |
| (15) | 5.4 Tra cuu lich su chat | Seller | Lich su chat voi buyer/admin |
| (16) | 5.4 Tra cuu lich su chat | Admin | Lich su chat voi seller |
| (17) | 5.3 Luu tin nhan | 5.5 Phat realtime SignalR | Tin nhan moi da duoc luu DB |
| (18) | 5.5 Phat realtime SignalR | Buyer | Event tin nhan moi neu buyer dang online |
| (19) | 5.5 Phat realtime SignalR | Seller | Event tin nhan moi neu seller dang online |
| (20) | 5.5 Phat realtime SignalR | Admin | Event tin nhan moi neu admin dang online |

Ghi chu: 5.2 phai chan chat Buyer-Admin. Phase phu nay khong xu ly unread count, online/offline indicator hay typing indicator.

## Kiem Tra Can Bang Voi Level 0

| Tien trinh Level 0 | Cac tien trinh Level 1 |
| --- | --- |
| 1.0 Quan ly tai khoan | 1.1 Tiep nhan thong tin tai khoan, 1.2 Xac thuc va phan quyen, 1.3 Tra cuu thong tin tai khoan |
| 2.0 Quan ly build keyboard | 2.1 Tra cuu danh muc va rule, 2.2 Xu ly cau hinh build, 2.3 Tinh tong gia snapshot, 2.4 Luu va tra cuu build |
| 3.0 Quan ly request build | 3.1 Tiep nhan yeu cau gui request, 3.2 Tao request build, 3.3 Tra cuu request, 3.4 Cap nhat trang thai request |
| 4.0 Quan tri he thong | 4.1 Quan ly user va role, 4.2 Quan ly seller, 4.3 Quan ly danh muc linh kien, 4.4 Tra cuu audit log |
| 5.0 Quan ly chat realtime | 5.1 Mo hoac tao hoi thoai, 5.2 Kiem tra quyen chat, 5.3 Luu tin nhan, 5.4 Tra cuu lich su chat, 5.5 Phat realtime SignalR |
