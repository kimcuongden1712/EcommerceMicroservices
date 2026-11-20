# 07 — Kiến trúc và dữ liệu

[Chỉ mục](README.md) · [Yêu cầu phi chức năng](08-nfr-load.md)

Giữ kiến trúc microservices của dự án. Dùng storefront và admin Blazor, Ocelot cho API client, RabbitMQ cho sự kiện và Docker Compose cho môi trường phát triển. Source TEDU được phép tái sử dụng có chọn lọc. Kiểm chứng build, cấu hình Identity và luồng tích hợp trước khi chọn thành phần đưa sang production.

| Thành phần | Sở hữu | Cơ sở dữ liệu |
|---|---|---|
| Identity | Danh tính, xác minh email, đăng nhập và quyền | SQL Server |
| Product | Danh mục, nội dung catalog, SKU, giá và ảnh | MySQL |
| Customer | Hồ sơ và địa chỉ | PostgreSQL |
| Basket | Giỏ hiện tại của Buyer | Redis |
| Inventory REST/gRPC | Tồn, giữ tồn và lịch sử biến động theo SKU | MongoDB |
| Ordering | Snapshot đơn, điều phối checkout, giao dịch Payment và vận đơn Shipping | SQL Server |
| Background Job | Lịch gửi thông báo, đồng bộ trạng thái hoặc đối soát định kỳ | MongoDB |

## Luồng tích hợp

1. Blazor truy cập các API qua Ocelot. Chỉ công khai endpoint cần cho khách và nhà cung cấp. Xác thực và kiểm tra quyền tại các API sở hữu nghiệp vụ.
2. Ordering chịu trách nhiệm điều phối checkout. Xác minh cách Saga Orchestrator của TEDU lưu tiến độ và phục hồi trước khi tái sử dụng. MVP phải lưu trạng thái điều phối bền vững.
3. Inventory thực hiện lệnh giữ/chốt/trả tồn có khóa nghiệp vụ. Một bước lỗi cần dẫn tới retry an toàn, bù trừ hoặc ngoại lệ có thể xử lý.
4. Định nghĩa hợp đồng event có phiên bản, correlation ID, mã đơn và khóa chống trùng. Lưu thay đổi nghiệp vụ và bản ghi phát sự kiện cùng giao dịch ở nơi phù hợp. Consumer ghi nhận sự kiện đã xử lý trước khi xác nhận hoàn thành.
5. Stripe và FedEx tích hợp qua module Payment và Shipping của Ordering. Đối tác timeout hoặc webhook đến muộn được đối soát bằng trạng thái phía đối tác.

## Dữ liệu và vận hành

Mỗi service sở hữu schema và quyền truy cập riêng. Tham chiếu chéo bằng ID và snapshot, tránh truy cập database service khác. Database, broker và dashboard hạ tầng nằm trong mạng nội bộ. Giá, phí và thuế được lưu ở USD cùng quy tắc làm tròn. Lưu timestamp UTC, hiển thị theo ngữ cảnh. Thiết kế bảo lưu danh tính nghiệp vụ của SKU và đơn khi sản phẩm ngừng bán.

Điểm cần xác minh từ source base gồm triển khai Identity thay cho phụ thuộc image mẫu nếu cần, lưu bền trạng thái Saga, cơ chế phát/nhận sự kiện, backup/restore và quyền chạy các image/phụ thuộc ở production.
