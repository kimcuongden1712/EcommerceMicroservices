# 04 — Thanh toán và vòng đời đơn

[Chỉ mục](README.md) · [Giữ tồn](03-checkout-inventory.md) · [Giao hàng](05-shipping-tax.md)

| ID | Yêu cầu | Tiêu chí chấp nhận |
|---|---|---|
| PAY-01 | Dùng Stripe Checkout hosted, thẻ tín dụng/ghi nợ, thu trước bằng USD. Sandbox cho dev/staging và Live cho production sau nghiệm thu. | Số tiền, tiền tệ và mã đơn khớp dữ liệu phía máy chủ. Hệ thống shop không tiếp nhận hoặc lưu số thẻ hay CVV. |
| PAY-02 | Xác nhận giao dịch qua webhook đã kiểm chữ ký và đối chiếu trạng thái, số tiền, tiền tệ, mã phiên và mã đơn. | Redirect chỉ hiển thị trạng thái. Webhook trùng hoặc sai thứ tự không tạo đơn, chốt tồn hoặc gửi email lần nữa. Giữ thông tin để đối soát sự kiện không khớp. |
| ORD-01 | Theo dõi đơn, thanh toán và giao hàng bằng các trạng thái riêng. | Đơn chỉ vào hàng đợi xuất kho khi đã thanh toán và chốt tồn. Nhãn được tạo không làm đơn chuyển sang đã giao hãng. |
| ORD-02 | Buyer hủy đơn chưa trả tiền. Với đơn đã trả tiền và chưa bàn giao hãng, Buyer gửi yêu cầu để Admin xử lý. | Khi thanh toán và hủy đến đồng thời, kết quả cuối khớp Stripe và Inventory. Đơn đã bàn giao đi vào luồng đổi trả/hỗ trợ. |
| PAY-03 | Admin duyệt hoàn toàn phần trong MVP, ghi lý do và khởi tạo qua Stripe. | Có thể tra cứu mã hoàn tiền và trạng thái chờ/thành công/thất bại. Retry không hoàn tiền lần hai. Không đánh dấu đã hoàn khi Stripe còn đang xử lý. |
| ORD-03 | Gửi email xác nhận đơn, gửi hàng và kết quả hủy/hoàn tiền. | Lỗi email không làm mất đơn. Có theo dõi trạng thái gửi và cơ chế gửi lại với khóa chống trùng. |

## Trạng thái và quy tắc chuyển

- **Đơn.** Chờ thanh toán → xác nhận → đang chuẩn bị → đã bàn giao → đã giao. Nhánh hủy áp dụng trước bàn giao. Ngoại lệ thanh toán hoặc tồn tạm khóa xuất kho để xử lý.
- **Thanh toán.** Chưa trả → đã trả, hoặc thất bại/hết hạn. Sau đã trả có thể sang hoàn tiền đang xử lý, đã hoàn hoặc hoàn thất bại. Một lần thử thất bại có thể thử lại theo chính sách lần checkout mới hoặc phiên còn hợp lệ.
- **Vận đơn.** Xem mục 05. Hủy trước bàn giao phải dừng xuất kho, xử lý nhãn đã tạo nếu cần và đối chiếu tồn còn bán được.
- **Đối soát.** Admin đối chiếu đơn với Stripe theo mã đơn và giao dịch. Danh sách ngoại lệ gồm tiền đã thu chưa chốt tồn, sai số tiền, chưa ghi nhận webhook và hoàn tiền lỗi. Lịch đối soát hằng ngày là đề xuất vận hành.

Tài liệu kỹ thuật [Stripe fulfillment](https://docs.stripe.com/checkout/fulfillment) và [Refunds](https://docs.stripe.com/refunds).
