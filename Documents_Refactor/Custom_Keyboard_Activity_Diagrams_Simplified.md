# Custom Keyboard Builder - Activity Diagrams Simplified

Tai lieu nay la ban rut gon cua `Custom_Keyboard_Activity_Diagrams_Refactor.md`.
Muc tieu la de trinh bay nhanh cac luong chinh theo ERD/FHD refactor, giu dung pham vi:

- Buyer tao build tu keyboard kit, gui request va co the nop don tro thanh seller.
- Seller xu ly request duoc gan.
- Admin quan tri user/seller profile/catalog/audit va duyet don seller.
- Chat chi ho tro Buyer-Seller va Seller-Admin/Admin-Seller.

## AD-S01: Tai Khoan

```mermaid
flowchart TD
    subgraph UserLane["Nguoi dung"]
        Start((Bat dau))
        OpenApp[Mo ung dung]
        HasAccount{Da co tai khoan?}
        Register[Buyer dang ky tai khoan]
        Login[Dang nhap]
        Profile[Xem profile tai khoan]
        Logout[Dang xuat]
        End((Ket thuc))
    end

    subgraph SystemLane["He thong"]
        ValidateAccount{Thong tin hop le?}
        ShowError[Hien thi loi]
        RouteByRole[Dieu huong dashboard theo role]
        ShowProfile[Hien thi profile]
        ReturnLogin[Quay ve man hinh dang nhap]
    end

    Start --> OpenApp --> HasAccount
    HasAccount -- Chua co / Buyer --> Register --> ValidateAccount
    HasAccount -- Da co --> Login --> ValidateAccount
    ValidateAccount -- Khong --> ShowError --> Login
    ValidateAccount -- Co --> RouteByRole --> Profile --> ShowProfile --> Logout --> ReturnLogin --> End
```

| FHD | Pham vi |
| --- | --- |
| 1.1 | Buyer tu dang ky tai khoan moi. |
| 1.2 | Buyer/Seller/Admin dang nhap. |
| 1.3 | Dang xuat. |
| 1.4 | Xem profile tai khoan. |

## AD-S02: Buyer Tao Build Va Gui Request

```mermaid
flowchart TD
    subgraph UserLane["Nguoi dung - Buyer"]
        Start((Bat dau))
        OpenDashboard[Mo dashboard buyer]
        Choose{Chon luong}
        ViewBuilds[Xem build da luu]
        CreateBuild[Tao build moi]
        SelectKit[Chon keyboard kit]
        AddItems[Them item/mod]
        ClickSave[Luu build]
        ChooseSeller[Chon seller va gui request]
        ViewStatus[Theo doi request]
        Archive[Luu tru build]
        ApplySeller[Nop don tro thanh seller]
        End((Ket thuc))
    end

    subgraph SystemLane["He thong"]
        ShowDashboard[Hien thi dashboard/build/request]
        LoadCatalog[Hien thi catalog theo kit]
        ValidateBuild{Build hop le?}
        ShowBuildError[Hien thi loi/canh bao]
        SaveBuild[Luu build va tong gia snapshot]
        CreateRequest[Tao request Pending]
        SaveApplication[Luu don seller Pending]
    end

    Start --> OpenDashboard --> ShowDashboard --> Choose
    Choose -- Xem build --> ViewBuilds
    ViewBuilds -- Luu tru --> Archive --> End
    ViewBuilds -- Gui request --> ChooseSeller
    Choose -- Tao build --> CreateBuild --> LoadCatalog --> SelectKit --> AddItems --> ClickSave --> ValidateBuild
    ValidateBuild -- Khong --> ShowBuildError --> AddItems
    ValidateBuild -- Co --> SaveBuild --> ChooseSeller --> CreateRequest --> ViewStatus --> End
    Choose -- Dang ky seller --> ApplySeller --> SaveApplication --> End
```

| FHD | Pham vi |
| --- | --- |
| 2.1-2.8 | Xem dashboard/build, tao build, chon kit, them item/mod, kiem tra va luu build. |
| 2.9-2.11 | Chon seller verified, gui request va theo doi trang thai. |
| 2.12 | Luu tru build. |
| 2.13 | Nop don tro thanh seller. |

## AD-S03: Seller Xu Ly Request

```mermaid
flowchart TD
    subgraph UserLane["Nguoi dung - Seller"]
        Start((Bat dau))
        OpenDashboard[Mo dashboard seller]
        ViewRequests[Xem request duoc gan]
        OpenDetail[Xem chi tiet request]
        Action{Chon xu ly}
        Accept[Chap nhan]
        Progress[Cap nhat dang xu ly]
        Complete[Hoan thanh]
        Cancel[Huy request]
        End((Ket thuc))
    end

    subgraph SystemLane["He thong"]
        ShowDashboard[Hien thi tong quan request]
        LoadRequest[Hien thi request/build snapshot]
        ValidateStatus{Trang thai hop le?}
        UpdateStatus[Cap nhat request]
        ShowResult[Hien thi ket qua]
    end

    Start --> OpenDashboard --> ShowDashboard --> ViewRequests --> OpenDetail --> LoadRequest --> Action
    Action -- Chap nhan --> Accept --> ValidateStatus
    Action -- Dang xu ly --> Progress --> ValidateStatus
    Action -- Hoan thanh --> Complete --> ValidateStatus
    Action -- Huy --> Cancel --> ValidateStatus
    ValidateStatus -- Co --> UpdateStatus --> ShowResult --> End
    ValidateStatus -- Khong --> ShowResult --> Action
```

| FHD | Pham vi |
| --- | --- |
| 3.1-3.3 | Xem dashboard, danh sach request va chi tiet request. |
| 3.4-3.7 | Chap nhan, cap nhat dang xu ly, hoan thanh hoac huy request. |

## AD-S04: Admin Quan Tri

```mermaid
flowchart TD
    subgraph UserLane["Nguoi dung - Admin"]
        Start((Bat dau))
        OpenDashboard[Mo dashboard admin]
        Choose{Chon chuc nang}
        ManageUsers[Quan ly user]
        ManageSellers[Quan ly seller profile]
        ManageCatalog[Quan ly catalog]
        ViewAudit[Xem audit log]
        ReviewApplications[Duyet don seller]
        End((Ket thuc))
    end

    subgraph SystemLane["He thong"]
        ShowDashboard[Hien thi tong quan quan tri]
        ValidateAction{Thao tac hop le?}
        SaveChange[Cap nhat du lieu]
        WriteAudit[Ghi audit neu can]
        ShowResult[Hien thi ket qua]
        ShowAudit[Hien thi audit log]
    end

    Start --> OpenDashboard --> ShowDashboard --> Choose
    Choose -- User --> ManageUsers --> ValidateAction
    Choose -- Seller profile --> ManageSellers --> ValidateAction
    Choose -- Catalog --> ManageCatalog --> ValidateAction
    Choose -- Don seller --> ReviewApplications --> ValidateAction
    Choose -- Audit log --> ViewAudit --> ShowAudit --> End
    ValidateAction -- Co --> SaveChange --> WriteAudit --> ShowResult --> Choose
    ValidateAction -- Khong --> ShowResult --> Choose
```

| FHD | Pham vi |
| --- | --- |
| 4.1 | Xem dashboard admin. |
| 4.2 | Quan ly user va role/active status. |
| 4.3 | Quan ly seller profile va verified status. |
| 4.4 | Quan ly catalog keyboard. |
| 4.5 | Xem audit log. |
| 4.6 | Duyet hoac tu choi don xin lam seller. |

## AD-S05: Chat

```mermaid
flowchart TD
    subgraph UserLane["Nguoi dung"]
        Start((Bat dau))
        OpenChat[Mo tab Chat]
        SelectCounterpart[Chon nguoi chat]
        TypeMessage[Nhap tin nhan]
        SendMessage[Gui tin nhan]
        End((Ket thuc))
    end

    subgraph SystemLane["He thong"]
        CheckPermission{Dung cap chat?}
        ShowError[Tu choi thao tac]
        LoadHistory[Load lich su tin nhan]
        SaveMessage[Luu tin nhan vao DB]
        ShowMessage[Hien thi tin nhan]
    end

    Start --> OpenChat --> SelectCounterpart --> CheckPermission
    CheckPermission -- Khong --> ShowError --> End
    CheckPermission -- Co --> LoadHistory --> TypeMessage --> SendMessage --> SaveMessage --> ShowMessage --> End
```

| FHD | Pham vi |
| --- | --- |
| 5.1 | Buyer chat voi seller. |
| 5.2 | Seller chat voi buyer. |
| 5.3 | Seller chat voi admin. |
| 5.4 | Admin chat voi seller. |

## Ranh Gioi

- Ban rut gon nay khong thay the ban chi tiet; dung de tong quan va trinh bay.
- Khong mo ta tung nut UI, DTO, service method, database column hoac realtime detail.
- Logic validation chi tiet van nam trong `Keyboard_Build_Validation_Logic.md`, DFD Level 2 va code service.
