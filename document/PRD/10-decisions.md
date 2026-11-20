# 10 — Sổ quyết định

[Chỉ mục](README.md)

| ID | Trạng thái | Quyết định hoặc thông tin cần bổ sung | Chủ sở hữu đề xuất | Mốc |
|---|---|---|---|---|
| D-01 | Đã chốt | Bán thật. Pháp nhân Mỹ tại California, một nhà bán, một kho California, USD, Mỹ khi ra mắt, quốc tế sau. | Product | Baseline |
| D-02 | Đã chốt | Ngày mục tiêu 01/01/2027. Ngân sách và nhân lực không đặt trần. | Sponsor | Baseline |
| D-03 | Đã chốt | Tồn theo SKU. Đăng nhập khi thêm giỏ. Checkout toàn bộ giỏ. Buyer/Admin và quản trị cốt lõi. | Product | Baseline |
| D-04 | Đã chốt | Tái sử dụng chọn lọc TEDU, giữ SQL Server, MySQL, PostgreSQL, Redis và MongoDB. | Architecture | Baseline |
| D-05 | Đề xuất | Stripe Checkout thu trước bằng thẻ. FedEx là hãng đầu tiên. Cần xác minh quyền production, hợp đồng, tuyến và dịch vụ. | Finance/Operations | Trước tích hợp production |
| D-06 | Mở | Thời hạn giữ tồn và phiên thanh toán, quy tắc cấp lại tồn khi tiền tới muộn, số lần thử và giới hạn số lượng. | Product/Ordering/Inventory | Trước nghiệm thu checkout |
| D-07 | Mở | Địa chỉ/ZIP kho, vùng giao cụ thể, PO Box và dịch vụ hãng. Phương án ban đầu là 48 bang liền kề và D.C. | Operations | Trước nghiệm thu báo giá |
| D-08 | Mở | Thị trường đăng ký thuế, cấu hình, phân loại sản phẩm và đối soát hoàn tiền. | Finance/Tax | Trước UAT thuế |
| D-09 | Mở | Chính sách hủy, đổi trả, phí giao khi hoàn, đầu mối hỗ trợ và nhu cầu tài khoản kho riêng. | Product/Operations | Trước công bố chính sách |
| D-10 | Đã chốt một phần | 100 triệu đồng thời là giả định thử tải cấp hệ thống. RPS, p95, checkout/giây, tỷ lệ lỗi và thời lượng đỉnh cho nghiệm thu mở bán còn mở. | Product/SRE/QA | Trước chạy tải nghiệm thu |
| D-11 | Mở | RPO, RTO, thời hạn lưu dữ liệu, giờ mở bán theo giờ California và người trực hỗ trợ. | Operations/SRE | Trước quyết định mở bán |
| D-12 | Đề xuất | Một kiện mỗi đơn, storefront tiếng Anh, Payment và Shipping là module của Ordering trong bản đầu. | Product/Architecture | Trước hoàn tất thiết kế |

Khi quyết định được duyệt, ghi ngày duyệt và người duyệt tại đây, cập nhật phần yêu cầu tương ứng và ca nghiệm thu bị ảnh hưởng.
