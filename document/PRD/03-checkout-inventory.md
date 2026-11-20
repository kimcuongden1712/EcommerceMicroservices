# 03 — Giỏ hàng, checkout và tồn kho

[Chỉ mục](README.md) · [Thanh toán](04-payment-orders.md)

| ID | Yêu cầu | Tiêu chí chấp nhận |
|---|---|---|
| CRT-01 | Buyer thêm, sửa số lượng, xóa SKU trong giỏ của mình. Mỗi SKU có một dòng. | Số lượng phải là số nguyên dương. Thêm lại cùng SKU cập nhật dòng hiện có. Thêm vào giỏ chưa giữ tồn. |
| CHK-01 | Checkout toàn bộ giỏ. Chọn địa chỉ giao, thông tin thanh toán và dịch vụ giao khả dụng. | Khi một SKU hết hàng, ngừng bán hoặc đổi giá, chỉ rõ dòng bị ảnh hưởng và yêu cầu khách xác nhận lại trước khi thanh toán. |
| CHK-02 | Máy chủ tính tổng bằng USD. Lưu snapshot SKU, tên, biến thể, số lượng, đơn giá, địa chỉ, phí giao và thuế. | Tổng hiển thị trên checkout, gửi Stripe và lưu đơn trùng nhau. Dùng số nguyên cents hoặc kiểu thập phân chính xác và quy tắc làm tròn thống nhất. |
| CHK-03 | Tạo đơn và phiên thanh toán theo khóa idempotency gắn với Buyer và lần checkout. | Gửi lại cùng yêu cầu trả về cùng kết quả. Yêu cầu thay đổi giỏ, địa chỉ hoặc giá cần tạo lần checkout mới theo quy tắc hết hạn của lần cũ. |
| INV-01 | Inventory sở hữu tồn theo SKU, số lượng giữ và số lượng khả dụng. | Hai khách cùng mua SKU cuối chỉ có tối đa một lượt giữ thành công. Đơn nhiều dòng phải giữ đủ toàn bộ trước khi mở thanh toán. Nếu giữ một phần rồi lỗi, giải phóng phần đã giữ. |
| INV-02 | Giữ tồn có thời hạn và mã duy nhất theo lần checkout. Áp dụng bán/giải phóng đúng một lần theo kết quả giao dịch. | Retry, timeout hoặc consumer restart không tạo thêm lượt giữ, bán hoặc trả tồn. Có thể truy xuất lịch sử theo SKU và đơn. |
| INV-03 | Hỗ trợ điều chỉnh tồn có lý do, người thực hiện và thời điểm. | Không chấp nhận điều chỉnh làm tồn khả dụng âm. Tồn trả về sau hủy/đổi trả chỉ tăng sau khi xác nhận hàng còn bán được. |

## Luồng và ngoại lệ

1. Buyer xác nhận giỏ, địa chỉ, phí và thuế. Ordering lưu đơn chờ và yêu cầu Inventory giữ tồn.
2. Sau khi giữ đủ, Ordering tạo phiên Stripe. Thời hạn phiên phải phối hợp với thời hạn giữ tồn. Thời hạn cụ thể thuộc [D-06](10-decisions.md).
3. Sự kiện thanh toán đã xác minh kích hoạt chốt tồn. Chỉ đưa đơn vào hàng đợi xuất kho khi thanh toán và tồn đều được xác nhận.
4. Khi hủy hoặc hết hạn, xác minh trạng thái phiên trước khi giải phóng. Nếu tiền đến sau khi lượt giữ đã hết, dừng xuất kho, thử cấp lại tồn theo quy tắc được duyệt hoặc chuyển Admin xử lý hoàn tiền.
5. Sau khi xác nhận thanh toán và tồn, loại phần giỏ đã mua theo snapshot để giữ lại thay đổi khách thực hiện sau khi bắt đầu checkout.
