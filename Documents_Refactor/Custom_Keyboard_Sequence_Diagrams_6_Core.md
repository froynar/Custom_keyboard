# Custom Keyboard Builder - 6 Core Sequence Diagrams

Tai lieu nay gom 6 sequence diagram rut gon de dung trong bao cao.
Muc tieu la trinh bay cac luong nghiep vu chinh, khong ve qua chi tiet cac ham noi bo.

---

## SD-R01: Account And Role Routing

```mermaid
sequenceDiagram
    participant User as User
    participant UI as Login Register UI
    participant Account as AccountService
    participant DB as SQL Server Database

    User->>UI: Dang ky hoac dang nhap
    UI->>Account: Gui thong tin tai khoan
    Account->>DB: Kiem tra hoac tao user
    DB-->>Account: Tra ve user va role

    alt Thong tin hop le
        Account-->>UI: Dang nhap thanh cong
        UI-->>User: Dieu huong den dashboard theo role
    else Thong tin khong hop le
        Account-->>UI: Loi xac thuc
        UI-->>User: Hien thi thong bao loi
    end
```

---

## SD-R02: Buyer Create Build And Send Request

```mermaid
sequenceDiagram
    participant Buyer as Buyer
    participant UI as Buyer Dashboard
    participant Catalog as CatalogService
    participant Build as BuildService
    participant Request as RequestService
    participant DB as SQL Server Database

    Buyer->>UI: Mo man hinh tao build
    UI->>Catalog: Lay danh sach linh kien kha dung
    Catalog->>DB: Doc catalog dang available
    DB-->>Catalog: Danh sach linh kien
    Catalog-->>UI: Du lieu catalog
    UI-->>Buyer: Hien thi linh kien de chon

    Buyer->>UI: Chon kit, switch, keycap va phu kien
    UI->>Build: Luu build
    Build->>DB: Luu build va cac linh kien da chon
    DB-->>Build: Build da luu
    Build-->>UI: Tra ve thong tin build

    Buyer->>UI: Gui request cho seller
    UI->>Request: Tao build request
    Request->>DB: Luu request voi trang thai Pending
    DB-->>Request: Request da tao
    Request-->>UI: Gui request thanh cong
    UI-->>Buyer: Thong bao da gui cho seller
```

---

## SD-R03: Seller Process Request And QC Summary

```mermaid
sequenceDiagram
    participant Seller as Seller
    participant UI as Seller Dashboard
    participant Request as RequestService
    participant QC as QC Device Layer
    participant DB as SQL Server Database

    Seller->>UI: Mo danh sach request
    UI->>Request: Lay request cua seller
    Request->>DB: Doc cac request lien quan
    DB-->>Request: Danh sach request
    Request-->>UI: Hien thi request

    Seller->>UI: Chap nhan request
    UI->>Request: Cap nhat trang thai Accepted
    Request->>DB: Luu trang thai moi
    DB-->>Request: Da cap nhat

    Seller->>UI: Bat dau xu ly va chay QC
    UI->>QC: Tao phien kiem tra
    QC->>DB: Luu ket qua QC tong hop
    DB-->>QC: QC summary da luu
    QC-->>UI: Tra ve ket qua QC
    UI-->>Seller: Hien thi pass / warning / fail

    alt QC dat
        Seller->>UI: Hoan thanh request
        UI->>Request: Cap nhat Completed
        Request->>DB: Luu trang thai Completed
    else QC khong dat
        UI-->>Seller: Yeu cau sua loi hoac chay QC lai
    end
```

---

## SD-R04: Admin Management

```mermaid
sequenceDiagram
    participant Admin as Admin
    participant UI as Admin Dashboard
    participant AdminService as AdminService
    participant DB as SQL Server Database

    Admin->>UI: Mo Admin Dashboard
    UI->>AdminService: Lay du lieu quan tri
    AdminService->>DB: Doc users, sellers, catalog va audit
    DB-->>AdminService: Du lieu dashboard
    AdminService-->>UI: Tra ve du lieu quan tri
    UI-->>Admin: Hien thi cac tab quan ly

    Admin->>UI: Them hoac sua linh kien
    UI->>AdminService: Luu thay doi catalog
    AdminService->>DB: INSERT/UPDATE catalog
    AdminService->>DB: Ghi audit log
    DB-->>AdminService: Da luu thay doi
    AdminService-->>UI: Cap nhat thanh cong
    UI-->>Admin: Hien thi catalog moi
```

---

## SD-R05: Seller Application Approval

```mermaid
sequenceDiagram
    participant Buyer as Buyer
    participant Admin as Admin
    participant UI as Custom Keyboard App
    participant SellerApp as SellerApplicationService
    participant DB as SQL Server Database

    Buyer->>UI: Nop don tro thanh seller
    UI->>SellerApp: Gui thong tin don
    SellerApp->>DB: Luu don voi trang thai Pending
    DB-->>SellerApp: Don da duoc luu
    SellerApp-->>UI: Nop don thanh cong
    UI-->>Buyer: Thong bao cho duyet

    Admin->>UI: Xem danh sach don seller
    UI->>SellerApp: Lay cac don Pending
    SellerApp->>DB: Doc seller applications
    DB-->>SellerApp: Danh sach don
    SellerApp-->>UI: Tra ve danh sach don

    Admin->>UI: Duyet don
    UI->>SellerApp: Approve application
    SellerApp->>DB: Cap nhat role va seller profile
    SellerApp->>DB: Ghi audit log
    DB-->>SellerApp: Da duyet thanh cong
    SellerApp-->>UI: Buyer da tro thanh seller
    UI-->>Admin: Hien thi ket qua duyet
```

---

## SD-R06: Chat Between Users

```mermaid
sequenceDiagram
    participant Starter as Buyer or Admin
    participant Seller as Seller
    participant UI as Chat UI
    participant Chat as ChatService
    participant DB as SQL Server Database

    Starter->>UI: Chon seller va mo chat
    UI->>Chat: StartBuyerConversation hoac StartAdminConversation
    Chat->>DB: Kiem tra seller verified va role cua Starter
    Chat->>DB: Lay hoac tao conversation
    DB-->>Chat: Conversation Buyer-Seller hoac Admin-Seller
    Chat-->>UI: Conversation san sang
    UI-->>Starter: Hien thi khung chat

    Starter->>UI: Gui tin nhan
    UI->>Chat: SendMessage
    Chat->>DB: Kiem tra sender la participant
    Chat->>DB: INSERT chat_messages va update conversation
    DB-->>Chat: Message da luu
    Chat-->>UI: Gui thanh cong

    Seller->>UI: Mo tab Chat
    UI->>Chat: GetConversations
    Chat->>DB: Lay conversation cua seller
    DB-->>Chat: Danh sach Buyer-Seller va Admin-Seller
    Chat-->>UI: Tra ve danh sach chat
    UI-->>Seller: Hien thi cac hoi thoai co san

    Seller->>UI: Chon conversation
    UI->>Chat: GetMessages
    Chat->>DB: Kiem tra seller la participant
    Chat->>DB: SELECT chat_messages
    DB-->>Chat: Lich su tin nhan
    Chat-->>UI: Tra ve lich su chat
    UI-->>Seller: Hien thi tin nhan

    Seller->>UI: Gui tin nhan tra loi
    UI->>Chat: SendMessage
    Chat->>DB: Kiem tra seller la participant
    Chat->>DB: INSERT chat_messages va update conversation
    DB-->>Chat: Message da luu
    Chat-->>UI: Gui thanh cong
```
