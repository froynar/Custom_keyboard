# Tóm tắt đoạn chat về báo cáo đồ án Custom Keyboard Builder

**Ngày cập nhật:** 24/07/2026  
**Dự án:** Custom Keyboard Builder

## 1. Mục đích của chuỗi trao đổi

Chuỗi trao đổi tập trung vào hai nội dung chính:

1. Giải thích kiến trúc phần mềm và các thuật ngữ kỹ thuật xuất hiện trong báo cáo.
2. Chỉnh lý cấu trúc, nội dung và hình thức của báo cáo đồ án dựa trên báo cáo mẫu.

Các thay đổi được thực hiện theo hướng tạo tài liệu Word riêng hoặc bản báo cáo chỉnh sửa để người dùng có thể kiểm tra và chèn thủ công vào tài liệu gốc khi cần.

## 2. Nội dung đã giải thích về kiến trúc phần mềm

### 2.1. Đoạn mô tả kiến trúc

Đoạn được thảo luận trong báo cáo:

> Ứng dụng được tổ chức theo MVVM kết hợp các lớp service và repository. MainWindow đóng vai trò composition root, khởi tạo kết nối SQL Server, repository, service, MQTT, DeviceSimulator và MainShellViewModel; dashboard được điều hướng theo role sau khi đăng nhập.

Ý nghĩa của các thành phần:

- **MVVM (Model – View – ViewModel):** Mẫu kiến trúc dùng để tách giao diện, trạng thái giao diện và dữ liệu/nghiệp vụ.
- **Model:** Biểu diễn dữ liệu và đối tượng nghiệp vụ, chẳng hạn người dùng, linh kiện, cấu hình bàn phím, yêu cầu lắp ráp và tin nhắn.
- **View:** Phần giao diện được khai báo bằng các tệp XAML.
- **ViewModel:** Thành phần trung gian cung cấp dữ liệu, trạng thái và lệnh cho View; đồng thời gọi Service để xử lý nghiệp vụ.
- **Service:** Xử lý quy tắc nghiệp vụ, kiểm tra dữ liệu, phân quyền và điều phối các thao tác.
- **Repository:** Đóng gói việc đọc/ghi dữ liệu trong SQL Server và ánh xạ dữ liệu thành Model.
- **Composition root:** Vị trí tập trung khởi tạo và liên kết các đối tượng mà ứng dụng cần. Trong dự án này, `MainWindow` đảm nhận vai trò đó.
- **SQL Server:** Hệ quản trị cơ sở dữ liệu dùng để lưu dữ liệu của hệ thống.
- **MQTT:** Giao thức gửi/nhận thông điệp nhẹ theo cơ chế publish/subscribe, phù hợp với dữ liệu thiết bị hoặc dữ liệu thời gian thực.
- **DeviceSimulator:** Thành phần mô phỏng thiết bị kiểm tra bàn phím khi chưa kết nối đầy đủ với phần cứng thật.
- **MainShellViewModel:** ViewModel chính quản lý khung giao diện và việc chuyển trang.
- **Dashboard:** Màn hình tổng quan sau khi đăng nhập.
- **Role:** Vai trò hoặc nhóm quyền của tài khoản, ví dụ Buyer, Seller và Admin.

Luồng hoạt động tổng quát: `MainWindow` tạo các thành phần cần thiết, kết nối chúng với nhau, sau đó `MainShellViewModel` căn cứ vào vai trò của tài khoản để đưa người dùng đến dashboard phù hợp.

### 2.2. Lý do bảng kiến trúc ban đầu không có Model

Bảng kiến trúc ban đầu liệt kê View, ViewModel, Service, Repository và Data/Realtime nhưng thiếu dòng Model. Điều này không có nghĩa dự án không dùng Model; bảng chỉ mô tả chưa đầy đủ.

Repository có trách nhiệm “ánh xạ model”, còn Service và ViewModel đều sử dụng các đối tượng dữ liệu này. Vì vậy, để thể hiện đúng MVVM, bảng nên bổ sung:

- **Lớp:** Model
- **Thành phần:** `Models/**/*.cs`
- **Trách nhiệm:** Biểu diễn dữ liệu và các đối tượng nghiệp vụ của hệ thống; được Repository, Service và ViewModel sử dụng.

### 2.3. “View” trong MVVM và `CREATE VIEW` trong SQL

Hai khái niệm này có cùng từ “View” nhưng không cùng chức năng:

- **View trong MVVM:** Giao diện mà người dùng nhìn thấy và thao tác.
- **SQL View được tạo bằng `CREATE VIEW`:** Một truy vấn được đặt tên trong cơ sở dữ liệu, hoạt động giống một bảng ảo.

Ví dụ, SQL View có thể tổng hợp dữ liệu từ nhiều bảng để phục vụ dashboard hoặc báo cáo. Tuy nhiên, nó không tạo giao diện và không thay thế View trong MVVM.

### 2.4. Quan hệ giữa Repository và SQL View

Việc bỏ Repository không đồng nghĩa phải dùng `CREATE VIEW`, và SQL View cũng không thay thế được Repository:

- Repository là lớp mã nguồn chịu trách nhiệm giao tiếp với cơ sở dữ liệu.
- SQL View chỉ là một đối tượng bên trong SQL Server.
- Repository vẫn có thể đọc dữ liệu từ bảng thường, stored procedure hoặc SQL View.

Trong dự án, SQL View phù hợp nhất với các truy vấn chỉ đọc, có nhiều phép nối hoặc tổng hợp lặp lại, chẳng hạn dữ liệu dashboard, thống kê, lịch sử và danh sách tổng hợp. Các thao tác thêm, sửa, xóa, kiểm tra quyền hoặc xử lý quy trình nghiệp vụ vẫn nên đi qua Repository và Service.

## 3. Yêu cầu chỉnh sửa cấu trúc báo cáo

Người dùng đã cung cấp:

- Báo cáo hiện tại: `Báo_Cáo_Codex.docx`
- Báo cáo mẫu: `Báo cáo KTPMUD (1).pdf`

Các yêu cầu chính:

### 3.1. Phần mở đầu

Phần mở đầu cần trình bày:

- Giới thiệu đồ án và lý do chọn đề tài.
- Mục đích của đề tài.
- Nhiệm vụ của đề tài.
- Phân công nhiệm vụ trong nhóm.
- Nội dung sơ lược của báo cáo, gồm số chương, nội dung từng chương, số trang, hình và bảng.

### 3.2. Chương 1

- Chương 1 mang tính tổng quan nhưng không đặt tên đề mục là “Tổng quan đề tài”.
- Chuyển các nội dung công nghệ, môi trường triển khai và kiến trúc phần mềm trước đây ở mục 2.1 và 2.2 về Chương 1.
- Giới thiệu chi tiết hơn về công nghệ, phần mềm, thư viện và kiến trúc được sử dụng.
- Mỗi chương có đoạn mở đầu ngắn.
- Cuối chương có phần kết luận và câu dẫn sang chương tiếp theo.
- Phần mô tả các lớp kiến trúc có thể chuyển từ dạng bảng sang các đoạn văn tương tự phần “Công nghệ và môi trường triển khai”.

### 3.3. Chương 2 – Phân tích và thiết kế hệ thống

- Tập trung vào phân tích và thiết kế hệ thống.
- Nêu rõ phần cứng, SQL Server và các công nghệ có liên quan.
- Giữ nguyên phần năm sơ đồ hiện có:
  - Use Case Diagram
  - Activity Diagram
  - Sequence Diagram
  - ERD tổng quát
  - ERD chi tiết

### 3.4. Chương 3 – Triển khai và kiểm thử

- Trình bày giao diện của ứng dụng.
- Đưa các đoạn mã tiêu biểu gắn với giao diện, ví dụ màn hình đăng nhập hoặc dashboard.
- Mã nguồn không được chèn dưới dạng ảnh.
- Mỗi đoạn mã được đặt trong bảng hai hàng:
  - Hàng đầu là tên đoạn mã, có nền xám.
  - Hàng thứ hai chứa mã nguồn bằng phông chữ monospace/console.

### 3.5. Kết luận báo cáo

Phần kết luận cần trả lời rõ:

- Nhiệm vụ đã hoàn thành đến mức nào.
- Kết quả có phù hợp với mục đích đề tài hay không.
- Những hạn chế còn tồn tại.
- Hướng phát triển tiếp theo.

## 4. Quy định về văn phong và trình bày

- Không sử dụng các đại từ như “em”, “tôi”, “chúng tôi”.
- Ưu tiên câu vô chủ hoặc cách diễn đạt khách quan, ví dụ: “Từ Hình 2.1 có thể thấy...”.
- Tiêu đề hình đặt bên dưới hình và căn giữa.
- Tiêu đề bảng đặt bên trên bảng, căn giữa hoặc căn trái.
- Hình và bảng được đánh số theo chương, ví dụ Hình 2.1, Hình 2.2 hoặc Bảng 3.1.
- Trong phần nội dung phải có câu dẫn hoặc tham chiếu đến hình và bảng tương ứng.
- Tránh để một trang mới bắt đầu trực tiếp bằng hình ảnh khi không có đoạn dẫn.
- Các ghi chú màu đỏ trong báo cáo cần được thực hiện, sau đó xóa hoặc để trống phần được yêu cầu.

## 5. Các tác nhân của hệ thống

Dựa trên nội dung dự án, ba tác nhân chính được xác định:

- **Buyer:** Lựa chọn linh kiện, tạo và lưu cấu hình bàn phím, gửi yêu cầu lắp ráp, theo dõi trạng thái và trao đổi với Seller.
- **Seller:** Tiếp nhận yêu cầu, quản lý quá trình xử lý/lắp ráp, thực hiện hoặc theo dõi kiểm tra chất lượng và trao đổi với Buyer.
- **Admin:** Quản lý tài khoản, danh mục dữ liệu nền, quyền truy cập và theo dõi hoạt động hệ thống.

Các tác nhân phải đăng nhập trước khi sử dụng các chức năng theo quyền tương ứng.

## 6. Nội dung bổ sung cho Chương 3

### 6.1. Mục 3.5 – Những hạn chế và lỗi còn tồn tại

Nội dung được điều chỉnh để thể hiện rõ nhược điểm của phiên bản hiện tại:

- Ứng dụng mới hoạt động dưới dạng WPF trên Windows và phải cài đặt trên từng máy.
- Ứng dụng kết nối trực tiếp tới SQL Server, chưa có Web API trung gian.
- Khả năng triển khai qua Internet, mở rộng nhiều người dùng và vận hành trên đám mây còn hạn chế.
- Chat mới lưu tin nhắn vào cơ sở dữ liệu, chưa hỗ trợ trao đổi tức thời bằng SignalR hoặc WebSocket.
- Quy trình QC còn dùng dữ liệu mô phỏng từ `DeviceSimulator`, chưa phản ánh đầy đủ phần cứng thật.
- Chưa hoàn thiện giỏ hàng, tồn kho, thanh toán, vận chuyển, theo dõi đơn hàng, giám sát hệ thống và kiểm thử tự động.

### 6.2. Mục 3.6 – Hướng phát triển trong tương lai

Các hướng phát triển chính:

- Phát triển chat trực tiếp theo thời gian thực.
- Bổ sung trạng thái online, đang nhập, đã nhận, đã đọc, gửi ảnh/tệp và thông báo tin nhắn.
- Xây dựng phiên bản web có thể truy cập qua Internet.
- Sử dụng ASP.NET Core Web API làm lớp trung gian.
- Phát triển giao diện responsive cho trình duyệt, điện thoại và máy tính bảng.
- Bổ sung quản lý tồn kho, giỏ hàng, thanh toán trực tuyến, vận chuyển và theo dõi đơn hàng.
- Kết nối ESP32, ma trận phím hoặc cảm biến thật; truyền dữ liệu kiểm tra qua MQTT.
- Triển khai trên máy chủ hoặc nền tảng đám mây; bổ sung HTTPS, sao lưu, logging, health check, cảnh báo và CI/CD.

## 7. Nội dung của phần kết luận đã viết lại

Phần kết luận đánh giá dự án theo hướng khách quan:

- Các nhiệm vụ phân tích, thiết kế, xây dựng mã nguồn và minh họa luồng nghiệp vụ cốt lõi đã được hoàn thành.
- Hệ thống phù hợp với mục tiêu số hóa quy trình cấu hình và gửi yêu cầu lắp ráp bàn phím tùy chỉnh.
- Phiên bản hiện tại đáp ứng mục đích học tập và minh họa kiến trúc, nhưng chưa phải sản phẩm vận hành hoàn chỉnh.
- Những nguyên nhân chính gồm cơ sở dữ liệu runtime chưa hoàn tất xác nhận migration, bộ kiểm thử tự động chưa được khôi phục đầy đủ và một số chức năng vẫn ở mức mô phỏng hoặc thử nghiệm.
- Hướng phát triển tập trung vào web, Internet, chat thời gian thực, quy trình thương mại đầy đủ và kết nối thiết bị kiểm tra thật.

## 8. Các tệp đã tạo trong quá trình làm việc

- `Bao_Cao_Codex_Chinh_Sua.docx`: Bản báo cáo đã được chỉnh lý.
- `Noi_Dung_Bo_Sung_Muc_3_5_3_6.docx`: Bản nội dung riêng ban đầu cho mục 3.5 và 3.6.
- `Noi_Dung_Bo_Sung_Muc_3_5_3_6_Chinh_Sua.docx`: Bản đã chỉnh sửa để nhấn mạnh hạn chế và hướng phát triển web/chat.
- `Ket_Luan_Bao_Cao_Custom_Keyboard_Builder.docx`: File Word riêng chứa phần kết luận đã viết lại.

Các tài liệu riêng được tạo để người dùng có thể chèn thủ công vào báo cáo chính; tệp báo cáo nguồn không bị ghi đè trong bước tạo phần kết luận.

## 9. Trạng thái hiện tại

- Nội dung kiến trúc và các thuật ngữ chính đã được giải thích.
- Sự khác nhau giữa View trong MVVM và SQL View đã được làm rõ.
- Cấu trúc báo cáo và các quy định trình bày đã được thống nhất.
- Nội dung mục 3.5, mục 3.6 và phần kết luận đã được tạo thành các file Word riêng.
- Bước tiếp theo có thể là chèn các nội dung đã duyệt vào báo cáo gốc, đồng bộ lại số mục, số hình, số bảng, mục lục và định dạng toàn bộ tài liệu.
