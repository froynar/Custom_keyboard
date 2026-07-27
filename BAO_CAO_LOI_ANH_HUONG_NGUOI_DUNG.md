# Báo cáo lỗi ảnh hưởng trực tiếp đến người dùng

Kiểm tra ngày 15/07/2026. Dự án chính build thành công (0 lỗi, 0 cảnh báo). Các lỗi dưới đây đã được đối chiếu từ luồng UI, ViewModel, service, repository và database đang được cấu hình.

| Mức độ | Lỗi | Ảnh hưởng ngắn gọn |
|---|---|---|
| Chặn | Code hiện dùng khóa chính tên `id`, nhưng database `CustomKeyboard_Refactor` đang dùng các tên cũ như `user_id`, `role_id`, `build_id`, `request_id`. Script kiểm tra chỉ-đọc dừng với lỗi `Invalid column name 'id'`. | Người dùng không thể đăng nhập/đăng ký hoặc sử dụng các dashboard; ứng dụng chỉ báo lỗi database chung. |
| Cao | Khóa tài khoản hoặc đổi role không vô hiệu hóa phiên đã đăng nhập; nhiều luồng ghi chỉ kiểm tra ID/quyền sở hữu, không kiểm tra lại `is_active` và role. | Buyer/seller đã bị khóa vẫn có thể tiếp tục lưu build, gửi request, cập nhật trạng thái hoặc chat cho đến khi đăng xuất. |
| Cao | Ở màn hình cấu hình, thao tác “Build mới” không xóa `SelectedBuild`, trong khi “Gửi request” luôn gửi `SelectedBuild.BuildId`. | Sau khi chọn một build cũ rồi tạo build mới, người dùng có thể vô tình gửi build cũ cho seller. |
| Cao | Các tác vụ tải khi đổi lựa chọn không có hủy/version-check (chat, QC, thống kê seller, danh mục admin). Kết quả của lựa chọn cũ có thể về sau và ghi đè lựa chọn mới. | Có thể hiển thị tin nhắn, kết quả QC hoặc số liệu của mục A dưới mục B; riêng chat có rủi ro lộ nội dung sai hội thoại. |
| Trung bình | Một số trường không kiểm tra độ dài trước khi truyền vào `SqlParameter` có kích thước cố định (đăng ký username/email, ghi chú request...). Đã xác nhận chuỗi 120 ký tự bị cắt còn 100 ký tự mà không báo lỗi. | Ứng dụng có thể báo đăng ký/gửi thành công nhưng dữ liệu bị mất; người dùng có thể không đăng nhập được bằng giá trị ban đầu. |
| Trung bình | MQTT được bật mặc định; khi không có broker, subscriber thử lại liên tục và mỗi lần đều append toàn bộ stack trace vào log, không có cơ chế xoay/giới hạn file. | Máy người dùng bị tăng dung lượng log liên tục và log lỗi thật bị chìm; file log hiện có đã khoảng 6,2 MB. |

Ghi chú: solution đầy đủ hiện không chạy được hai bộ verification vì thư mục `Phase6Verification` và `WpfUiVerification` đã bị xóa trong working tree. Không có mã nguồn hay dữ liệu nào được chỉnh sửa trong quá trình kiểm tra.
