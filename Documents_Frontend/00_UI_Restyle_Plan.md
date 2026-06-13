# UI Restyle Plan — Reskin WPF theo phong cách Gentelella

**Ngày lập:** 13/06/2026
**Nhánh:** `feat/frontend-templates` (mốc khôi phục: tag `mvp-stable-prefrontend`).

## Mục tiêu
Làm **giao diện WPF hiện tại đẹp/hiện đại hơn**, lấy template **Gentelella** (`frontend_template/gentelella/`) làm **mẫu thị giác** (màu sắc, bố cục sidebar, card, bảng, badge). **KHÔNG** chuyển sang web, **KHÔNG** WebView2/API.

## Nguyên tắc bất di bất dịch
- **Giữ nguyên 100%**: kiến trúc, `Models/Services/Repositories/Realtime/DB`, và **ViewModels + Commands + Binding**.
- Chỉ sửa **XAML (Views) + thêm Styles/Resources**. Cùng lắm thêm vài property thuần hiển thị cho biểu đồ (phần Analytics).
- **Không đổi tên** property/command mà View đang bind (tránh vỡ UI).
- Làm **từng màn hình một**, chạy app sau mỗi bước (lịch sử Phase 5 từng crash do XAML — `Run.Text TwoWay`).
- WPF không giống Bootstrap pixel-perfect, nhưng đạt được **look admin sạch, hiện đại** kiểu Gentelella.

---

## 1. Design system (theme dùng chung)

Tạo `Themes/` chứa `ResourceDictionary`, merge trong `App.xaml`:
```
Themes/
├─ Colors.xaml      (bảng màu + brushes)
├─ Typography.xaml  (font size/weight chuẩn)
└─ Controls.xaml    (Style cho Button/TextBox/ComboBox/DataGrid/ListBox/TabControl/Card/Badge/ScrollBar)
```

### Bảng màu (lấy theo Gentelella)
| Token | Hex | Dùng cho |
|---|---|---|
| `SidebarBg` | `#2A3F54` | nền sidebar trái (tối) |
| `SidebarText` | `#ECF0F1` | chữ menu |
| `Accent` | `#1ABB9C` | màu nhấn (nút primary, active, line) |
| `BodyBg` | `#F7F7F7` | nền vùng nội dung |
| `CardBg` | `#FFFFFF` | nền card/panel |
| `Border` | `#E6E9ED` | viền card/bảng |
| `TextPrimary` | `#3A3F44` | chữ chính |
| `TextMuted` | `#73879C` | chữ phụ/label |
| Status: success `#26B99A` · info `#3498DB` · warning `#F39C12` · danger `#E74C3C` · default `#BDC3C7` | | badge trạng thái |

### Style cần định nghĩa (implicit + keyed)
- **Button**: `PrimaryButton` (accent), `SecondaryButton` (viền), `DangerButton`; bo góc ~4px, hover/pressed states.
- **TextBox / PasswordBox / ComboBox**: viền nhạt, focus đổi viền accent, padding thoáng.
- **DataGrid**: header nền nhạt, hàng zebra, không gridline thừa, selection accent nhạt.
- **ListBox card**: item dạng **card** (nền trắng, viền `Border`, padding, bo góc) thay item phẳng.
- **TabControl**: tab kiểu pill/underline accent.
- **Card/Panel**: `ContentControl` style nền `CardBg` + viền + shadow nhẹ (`DropShadowEffect` mờ) + tiêu đề.
- **Badge (status)**: `Border` bo tròn + màu theo trạng thái qua `DataTrigger`/converter (Pending/Accepted/In_progress/Completed/Cancelled).
- **ScrollBar**: mảnh, màu xám nhạt.
- **Icon**: Gentelella dùng FontAwesome → WPF dùng **Segoe Fluent/MDL2 Icons** (có sẵn Win11) hoặc NuGet `FontAwesome.Sharp`. Ưu tiên Segoe (không thêm dependency).

---

## 2. Dashboard shell (bố cục chung)

Restyle `MainWindow` + 3 role view theo khung Gentelella:
```
┌───────────┬───────────────────────────────────────┐
│  SIDEBAR  │  TOPBAR: tiêu đề · user · [Logout]     │
│  (tối)    ├───────────────────────────────────────┤
│  logo     │                                       │
│  ─────    │   CONTENT (card/panel, scroll)         │
│  menu     │   - KPI cards / charts / bảng / form   │
│  theo role│                                       │
│  ─────    │                                       │
│  user     │                                       │
└───────────┴───────────────────────────────────────┘
```
- **Sidebar**: logo + danh sách mục theo role (Buyer/Seller/Admin menu khác nhau) + khối user dưới cùng. Mục active có line accent.
- **Topbar**: tiêu đề trang + tên user/role + nút Logout (bind `LogoutCommand` sẵn có).
- **Content**: các màn hình hiện tại đặt trong card/panel; chat vẫn nhúng như hiện tại nhưng style lại.

> Các role view (`BuyerDashboardView`/`SellerDashboardView`/`AdminDashboardView`) hiện đã là một `UserControl` swap trong `MainWindow` — chỉ cần **bọc khung shell** + restyle nội dung, giữ nguyên DataContext.

---

## 3. Màn hình → mẫu Gentelella tham khảo

| View WPF | Lấy look từ trang Gentelella | Trọng tâm restyle |
|---|---|---|
| `LoginView` / `RegisterView` | `login.html` / `register.html` | card giữa màn, input + nút accent |
| Shell + `MainWindow` | `index.html` (sidebar+topbar) | sidebar tối + topbar + content card |
| `BuyerDashboardView` (configurator + builds + requests) | `form_wizards` + `e_commerce`/`product_detail` + `tables_dynamic` | configurator dạng card/wizard; builds/requests = DataGrid đẹp + badge |
| `SellerDashboardView` (requests + status) | `orders`/`order_detail` + `index` (KPI) | bảng đơn + badge + nút trạng thái; thêm KPI/chart (mục 4) |
| `AdminDashboardView` (users/sellers/catalog/audit) | `user_management` + `tables_dynamic` + `form` | tab→nav, bảng + form gọn |
| `ChatView` | `chat.html` | bong bóng tin 2 phía, danh sách hội thoại |

> Quy tắc: **copy phong cách** (màu/spacing/card/table), **không** copy code HTML. Mỗi view giữ binding cũ, chỉ thay template/style.

---

## 4. Phase (chỉ UI — mỗi phase 1 commit, app luôn chạy được)

### U0 — Design tokens + base styles
- Tạo `Themes/Colors|Typography|Controls.xaml`, merge vào `App.xaml`.
- Restyle `LoginView`/`RegisterView` làm proof of concept.
- **Done:** app chạy, login/register có look mới; các view khác chưa đổi vẫn hoạt động.

### U1 — Dashboard shell
- Dựng sidebar + topbar + content scaffold cho `MainWindow`; áp khung cho 3 role view (chưa cần đẹp từng chi tiết).
- Menu sidebar theo role; Logout/topbar bind command sẵn có.
- **Done:** 3 role vào đúng khung shell mới, điều hướng/đăng xuất chạy.

### U2 — Buyer
- Configurator → card/wizard; builds + requests → DataGrid styled + **badge trạng thái**; thông báo (StatusMessage) hiển thị đẹp.
- **Done:** luồng buyer y nguyên, nhìn hiện đại.

### U3 — Seller
- Danh sách đơn → bảng đẹp + badge; nút state machine; xem snapshot.
- **Done:** luồng seller y nguyên, look mới (analytics ở U5).

### U4 — Admin
- Tab → nav/section; users/sellers/catalog/audit → bảng + form gọn.
- **Done:** quản trị đầy đủ, look mới.

### U5 — Analytics (LiveCharts2)
- Thêm NuGet **LiveChartsCore.SkiaSharpView.WPF**; thêm `StatsService` + query (xem `01_Analytics_Spec.md`).
- Seller dashboard: KPI cards + biểu đồ doanh thu/đơn (tháng/quý/năm) + donut trạng thái. Admin: tổng quan + top sellers. Buyer: thẻ stats công khai của seller khi chọn.
- **Done:** biểu đồ chạy với dữ liệu thật từ DB.

### U6 — Polish & ảnh
- Đồng bộ spacing/icon/empty-loading state; rà toàn bộ; chụp screenshot cho demo (cập nhật `Phase9_Demo_Script`).

---

## 5. Rủi ro & lưu ý
| Rủi ro | Giảm thiểu |
|---|---|
| Sửa XAML nhiều → crash render | Làm từng view, **chạy app sau mỗi view**; tránh đụng binding |
| Lỡ đổi tên property/command | Chỉ sửa template/style; không động ViewModel |
| LiveCharts2 thêm dependency | Chỉ ở U5; là lib WPF/.NET phổ biến, MVVM-friendly |
| Look không giống Bootstrap 100% | Chấp nhận; nhắm "admin sạch hiện đại", không pixel-perfect |
| `Phase6Verification` (logic) | Không ảnh hưởng (chỉ đổi UI) — vẫn phải xanh 15/15 |

## 6. Definition of Done
- 3 role có look admin hiện đại (sidebar + topbar + card + bảng + badge) theo phong cách Gentelella.
- Toàn bộ chức năng cũ **giữ nguyên** (binding/command không đổi); `Phase6Verification` vẫn **15/15**.
- Seller có dashboard biểu đồ; Buyer xem stats seller; Admin có tổng quan (LiveCharts2).
- Không đổi schema/kiến trúc; có screenshot demo.
