# 06 — Quản trị và vận hành

[Chỉ mục](README.md) · [Thanh toán và đơn](04-payment-orders.md) · [Phát hành](09-release.md)

| ID | Yêu cầu | Tiêu chí chấp nhận |
|---|---|---|
| ADM-01 | Quản lý danh mục, danh mục con, sản phẩm, ảnh, giá, nội dung và trạng thái công khai. | Có thể lưu nháp và ngừng bán. Ngừng bán không làm mất dữ liệu trên đơn cũ. |
| ADM-02 | Quản lý SKU duy nhất, biến thể màu/size, giá áp dụng và thông số đóng gói. | Mỗi biến thể hoạt động có SKU duy nhất. Không thể mở bán khi thiếu dữ liệu bắt buộc. |
| ADM-03 | Nhập và điều chỉnh tồn với lý do. | Có lịch sử người thao tác, thời điểm, SKU, số lượng trước/sau. Kiểm tra đồng thời với checkout. |
| ADM-04 | Tìm đơn theo mã, lọc theo trạng thái, xem khoản thanh toán, giữ tồn và vận đơn. | Admin nhìn thấy ngoại lệ và thao tác hợp lệ theo trạng thái. Các trường tài chính của đơn đã thanh toán được bảo toàn. |
| ADM-05 | Chuẩn bị kiện, tạo nhãn, xác nhận bàn giao và xử lý lỗi giao hàng. | Chỉ đơn đã xác nhận được tạo nhãn. Thao tác có nhật ký và có thể đối chiếu với mã vận đơn. |
| ADM-06 | Chấp nhận hủy và hoàn tiền, ghi lý do, xem kết quả đối soát. | Buyer không gọi được chức năng quản trị. Mỗi khoản hoàn tiền truy tới người thực hiện và mã Stripe. |
| ADM-07 | Công bố chính sách và kênh hỗ trợ. | Có trang tiếng Anh cho điều khoản, bảo mật, giao hàng và đổi trả. Buyer có thể liên hệ và cung cấp mã đơn. |

## Vận hành ngày đầu

Chuẩn bị hàng đợi đơn lỗi, hướng dẫn tra cứu giao dịch và vận đơn, người xử lý sự cố và quyền Admin được cấp theo danh tính cá nhân. Bật MFA cho tài khoản Admin theo yêu cầu thiết kế. Đối soát các đơn chờ thanh toán, đã thu tiền chưa xuất kho, hoàn tiền đang chờ và vận đơn chưa cập nhật. Nếu cần tài khoản kho độc lập trước ra mắt, giới hạn quyền theo ADM-03 và ADM-05, không cấp quyền hoàn tiền.
