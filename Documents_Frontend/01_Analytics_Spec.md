# Analytics Spec — Seller / Buyer / Admin (WPF + LiveCharts2)

Thống kê + biểu đồ **trong WPF** (không web). Số liệu **dẫn xuất từ bảng có sẵn** (`build_requests` + `builds` + `seller_profiles` + `keyboard_kits`) → **không đổi schema**. Thêm: `StatsService` (đọc-only) + render bằng **LiveCharts2**, bind theo **MVVM**.

> Cột dùng — `build_requests(seller_user_id, status, requested_at, accepted_at, completed_at, build_id)`; `builds(build_id, buyer_id, kit_id, total_cost_snapshot)`; `seller_profiles(user_id, is_verified, verified_at)`. Trạng thái: `Pending/Accepted/In_progress/Completed/Cancelled`.

---

## 1. Seller — dashboard riêng (trong `SellerDashboardView`)

Quyền: seller chỉ xem của **chính mình** (`sellerUserId = CurrentUser.UserId`).

### KPI (thẻ số)
| Chỉ số | Định nghĩa |
|---|---|
| **Tổng sale (doanh thu)** | `SUM(b.total_cost_snapshot)` các request `Completed` |
| **Tổng sản phẩm làm ra** | `COUNT(*)` request `Completed` |
| **Tổng khách hàng** | `COUNT(DISTINCT b.buyer_id)` |
| **Đơn đang xử lý** | `COUNT(*)` status ∈ {Pending, Accepted, In_progress} |
| (tùy chọn) **TG hoàn thành TB** | `AVG(DATEDIFF(day, accepted_at, completed_at))` đơn Completed |

### Biểu đồ (LiveCharts2)
| Biểu đồ | Control LiveCharts2 | Dữ liệu |
|---|---|---|
| Doanh thu & số đơn theo thời gian (toggle Tháng/Quý/Năm) | `CartesianChart` (`LineSeries`/`ColumnSeries`, 2 series) | nhóm theo `completed_at` |
| Tỉ lệ trạng thái đơn | `PieChart` (donut) | `COUNT GROUP BY status` |
| (tùy chọn) Top kit bán chạy | `CartesianChart` (`ColumnSeries` ngang) | `GROUP BY b.kit_id`, join `keyboard_kits` |

### Query mẫu (doanh thu/đơn theo tháng)
```sql
SELECT YEAR(br.completed_at) AS y, MONTH(br.completed_at) AS m,
       COUNT(*) AS orders, SUM(b.total_cost_snapshot) AS revenue
FROM build_requests br
JOIN builds b ON b.build_id = br.build_id
WHERE br.seller_user_id = @sellerId AND br.status = 'Completed' AND br.completed_at IS NOT NULL
GROUP BY YEAR(br.completed_at), MONTH(br.completed_at)
ORDER BY y, m;
```
- **Quý:** `DATEPART(QUARTER, br.completed_at)`. **Năm:** `YEAR(br.completed_at)`.
- **Khách:** `COUNT(DISTINCT b.buyer_id)`. **Trạng thái:** `GROUP BY br.status`.

---

## 2. Buyer — xem stats công khai của seller

Hiển thị trên **thẻ seller** ở bước chọn seller (trong `BuyerDashboardView`). **Không lộ doanh thu** — chỉ chỉ số uy tín/năng lực:

| Chỉ số | Định nghĩa |
|---|---|
| Số sản phẩm đã làm | `COUNT(*)` Completed |
| Tổng đơn nhận | `COUNT(*)` mọi status |
| Số khách đã phục vụ | `COUNT(DISTINCT b.buyer_id)` |
| Verified | `seller_profiles.is_verified` (badge) |
| Thành viên từ | `seller_profiles.verified_at` |
| (tùy chọn) TG hoàn thành TB | như trên |

> Chỉ cần thẻ số + badge (không cần chart đầy đủ) để buyer so sánh seller.

---

## 3. Admin — tổng quan hệ thống (trong `AdminDashboardView`)

| Khối | Dữ liệu | Biểu đồ |
|---|---|---|
| KPI hệ thống | users theo role, sellers verified, tổng builds, tổng request | thẻ số |
| Đơn theo trạng thái (toàn sàn) | `GROUP BY status` | PieChart |
| Doanh thu toàn sàn theo thời gian | `SUM(total_cost_snapshot)` Completed, nhóm tháng/quý/năm | CartesianChart line |
| Top sellers | theo doanh thu / số sản phẩm | ColumnSeries ngang |

---

## 4. API mới (`IStatsService`) + binding MVVM

```csharp
public enum StatsPeriod { Monthly, Quarterly, Yearly }

public interface IStatsService
{
    Task<SellerDashboardStats> GetSellerDashboardAsync(int sellerUserId, StatsPeriod period, CancellationToken ct = default);
    Task<SellerPublicStats>    GetSellerPublicAsync(int sellerUserId, CancellationToken ct = default);
    Task<AdminOverviewStats>   GetAdminOverviewAsync(StatsPeriod period, CancellationToken ct = default);
}
```
- **DTO:** `SellerDashboardStats { Kpis, TimeSeries[] (label, orders, revenue), StatusBreakdown[] (status,count), TopKits[] }`; `SellerPublicStats { productsMade, totalOrders, customers, isVerified, verifiedAt, avgCompletionDays }`; `AdminOverviewStats { kpis, statusBreakdown[], revenueSeries[], topSellers[] }`.
- **Repository:** `IStatsRepository` (hoặc mở rộng `IRequestRepository`) chứa aggregate query — **đọc-only**.
- **ViewModel:** thêm vào `SellerDashboardViewModel` / `AdminDashboardViewModel` (và `BuyerDashboardViewModel` cho seller-cards):
  - property KPI (`TotalRevenue`, `ProductsMade`, `TotalCustomers`...),
  - `ISeries[] RevenueSeries` / `StatusSeries` + `Axis[]` cho LiveCharts2 (bind vào `CartesianChart.Series`/`PieChart.Series`),
  - `SelectedPeriod` (Monthly/Quarterly/Yearly) → đổi gọi `StatsService` reload series,
  - load trong `InitializeAsync`/khi mở tab Analytics.
- **DI:** đăng ký `StatsService` + repo trong `MainWindow.xaml.cs` (giống các service khác).

---

## 5. Test (thêm vào `Phase6Verification`)
- Doanh thu/đơn/khách của seller tính đúng trên seed (đối chiếu tay vài seller).
- `GetSellerPublicAsync` **không** trả doanh thu; chỉ field công khai.
- Phân quyền: seller A không lấy dashboard seller B; buyer không gọi admin stats (ép ở ViewModel/service).
- Period Monthly/Quarterly/Yearly nhóm đúng.

> Seed có sẵn vài `build_requests` (Completed/khác) → số liệu demo khác 0. Muốn biểu đồ "đẹp" hơn có thể seed thêm vài đơn Completed rải theo tháng (tùy chọn, ghi rõ là seed demo).
