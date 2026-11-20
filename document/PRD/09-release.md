# 09 — Nghiệm thu và phát hành

[Chỉ mục](README.md) · [Sổ quyết định](10-decisions.md)

Mốc mở bán mục tiêu **01/01/2027**. Giờ mở bán California chờ quyết định. Nhân lực và ngân sách không đặt trần. Các nhóm storefront, đơn/tồn, tích hợp, hạ tầng và QA có thể triển khai song song theo hợp đồng API đã thống nhất.

| Thời gian mục tiêu | Kết quả |
|---|---|
| 20–27/11/2026 | Chốt vùng giao, địa chỉ kho, đầu mối thuế. Xác minh tài khoản Stripe/FedEx, chạy source base và thống nhất hợp đồng API/event. |
| 28/11–11/12 | Luồng Buyer từ catalog đến giỏ, checkout Stripe Sandbox, giữ tồn và lịch sử đơn. |
| 12–20/12 | Admin, FedEx, tracking, email, thuế và hoàn tiền. Có môi trường staging liên thông và chuẩn bị production. |
| 21–27/12 | UAT, đối soát, restore, load test và kiểm tra một giao dịch/hoàn tiền Live cùng một vận đơn thực sau khi có quyền vận hành. |
| 28–31/12 | Sửa lỗi chặn, nghiệm thu, chuẩn bị hỗ trợ và quyết định mở bán. |

## Ca nghiệm thu tối thiểu

| Ca | Điều kiện đạt | Yêu cầu |
|---|---|---|
| UAT-01 | Visitor chọn SKU, đăng nhập, mua bằng thẻ, nhận email, thấy đơn và tracking. | CAT-02, ACC-03, CHK-02, PAY-01, ORD-03, SHP-04 |
| UAT-02 | Hai Buyer tranh SKU cuối, tối đa một đơn được giữ thành công. Nếu giỏ nhiều SKU giữ lỗi giữa chừng, tồn được trả hoặc vào trạng thái phục hồi có thể theo dõi. | INV-01, INV-02 |
| UAT-03 | Checkout lặp và webhook giao lại/sai thứ tự vẫn chỉ có một hiệu ứng nghiệp vụ. Test cả thanh toán đến muộn sau khi giữ tồn hết hạn. | CHK-03, PAY-02, INV-02 |
| UAT-04 | Một địa chỉ được hỗ trợ, một ngoài vùng, một lỗi báo giá. Chỉ trường hợp đầu được thanh toán với tổng đã xác định. | SHP-01, SHP-02, TAX-01 |
| UAT-05 | Tạo vận đơn, phát lại do timeout, xem nhãn/tracking, xác nhận bàn giao và mốc giao. | SHP-03, SHP-04 |
| UAT-06 | Hủy đơn chưa thanh toán, hủy đã trả trước bàn giao, hoàn tiền lỗi/chờ và chuyển đơn đã bàn giao sang hỗ trợ. | ORD-02, PAY-03 |
| UAT-07 | Buyer không xem đơn người khác hoặc thao tác Admin. Admin thao tác tồn và hoàn có nhật ký. | ACC-04, ADM-03, ADM-06, NFR-03 |
| UAT-08 | Đạt bộ ngưỡng tải mở bán đã duyệt. Restore dữ liệu quan trọng và đối soát trạng thái. | NFR-01, NFR-04, NFR-05 |

## Mở bán và phục hồi

Product/Operations duyệt nghiệp vụ và nội dung công khai. QA duyệt ca bắt buộc. Platform/SRE duyệt khả năng vận hành và phục hồi. Đầu mối tài chính xác nhận cấu hình thuế và đối soát. Các lỗi chặn gồm thu tiền sai, bán vượt tồn, đơn không phục hồi được, không tạo được vận đơn trên tuyến mở và thiếu phương án khôi phục.

Khi sự cố sau phát hành ảnh hưởng đến tiền hoặc tồn, tạm ngưng checkout, giữ webhook và đối soát đang chạy, kiểm tra các đơn đang xử lý rồi rollback ứng dụng theo quy trình tương thích schema. Khôi phục mở bán sau khi xác nhận tính đúng của thanh toán, tồn và vận đơn.
