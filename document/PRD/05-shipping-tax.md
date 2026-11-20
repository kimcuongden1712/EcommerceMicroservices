# 05 — Giao hàng và thuế

[Chỉ mục](README.md) · [Đơn hàng](04-payment-orders.md) · [Quyết định mở](10-decisions.md)

Kho xuất duy nhất tại California. Ra mắt ở Mỹ với danh sách vùng giao cấu hình được. Đề xuất 48 bang liền kề và Washington, D.C. khi ra mắt. FedEx là hãng đề xuất duy nhất, chờ xác minh hợp đồng, dịch vụ và tài khoản production. Giao quốc tế và quy tắc DDP/DAP thuộc đợt sau.

| ID | Yêu cầu | Tiêu chí chấp nhận |
|---|---|---|
| SHP-01 | Kiểm tra quốc gia, bang, ZIP và dịch vụ hỗ trợ trước thanh toán. | Địa chỉ ngoài vùng hoặc không có dịch vụ trả lý do và không thể thanh toán. Quy tắc PO Box chờ xác nhận. |
| SHP-02 | Báo giá từ địa chỉ kho, địa chỉ giao, trọng lượng, kích thước kiện và dịch vụ. | Hiển thị phí USD và dự kiến giao nếu có. Đổi địa chỉ, giỏ hoặc báo giá hết hạn phải tính lại. Lỗi hãng được hiển thị và Buyer có thể thử lại. |
| SHP-03 | Admin tạo nhãn và vận đơn sau khi đơn đủ điều kiện xuất kho. Đề xuất một kiện mỗi đơn. | Lưu mã lô hàng nội bộ, tracking ID và nhãn. Nếu yêu cầu tạo nhãn bị timeout, đối chiếu trước khi thử lại để tránh hai vận đơn. |
| SHP-04 | Đồng bộ các mốc có nhãn, đã bàn giao, đang vận chuyển, đã giao và ngoại lệ. | Buyer thấy mốc gần nhất và tracking ID. Cập nhật trùng hoặc đến muộn không làm đơn quay ngược trạng thái. Hãng lỗi tạm thời không xóa trạng thái trước đó. |
| TAX-01 | Đề xuất dùng Stripe Tax ở các khu vực được chủ doanh nghiệp xác nhận cấu hình. | Tách tiền hàng, phí giao và thuế. Tính lại khi địa chỉ hoặc giỏ thay đổi. Nếu không xác định được thuế cần áp dụng, dừng checkout và ghi lỗi. |
| TAX-02 | Giữ snapshot giao dịch dùng để đối soát. | Lưu vùng thuế, tiền thuế, mã kết quả tính nếu có và điều chỉnh liên quan tới hoàn tiền. Báo giá giao hàng và tổng thanh toán gắn cùng phiên bản checkout. |

Dữ liệu tối thiểu gồm địa chỉ kho, SKU, trọng lượng và quy cách đóng gói, mã dịch vụ hãng, số vận đơn và báo giá. Trước khi mở quốc tế, bổ sung xuất xứ, mã HS, mô tả hải quan và quy tắc thuế/phí nhập khẩu theo tuyến.

Tài liệu kỹ thuật [FedEx Developer Portal](https://developer.fedex.com/api/en-us/home.html) và [Stripe Tax registrations](https://docs.stripe.com/tax/registering).
