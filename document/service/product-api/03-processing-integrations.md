# 03 — Xử lý và tích hợp

[Chỉ mục Product API](README.md) · [API contract](02-api-contract.md)

## Kiến trúc code đề xuất

Một project ASP.NET Core `src/Services/Product.API` theo cách tổ chức TEDU. Dùng EF Core với provider MySQL đã kiểm tra tương thích. Tách trách nhiệm theo thư mục, giữ module gọn trong một deployable.

| Thư mục | Nội dung |
|---|---|
| Controllers | PublicCatalog, AdminProducts, AdminCategories, InternalCatalog. Binding, authorization, HTTP status. |
| Features | Use case đọc/ghi catalog theo Products, Categories, Variants, Images. Validation nghiệp vụ và ranh giới transaction. |
| Entities và Persistence | Mô hình, EF mappings, migration, truy vấn projection và concurrency token. |
| Integrations | Object storage, phát outbox RabbitMQ, adapter hạ tầng có interface tại điểm cần thay thế. |
| Contracts | Request/response và mã lỗi theo API v1. Không serialize trực tiếp entity. |

## Luồng đọc catalog

1. Chuẩn hóa tham số, lọc sản phẩm công khai và tạo tập variant ứng viên phụ thuộc màu, size và miền giá. Nếu tìm SKU, sản phẩm phải có SKU khớp trong tập variant đủ điều kiện. Nếu tìm theo tên, tập variant giữ các bộ lọc đã áp dụng.
2. Gom theo sản phẩm, tính giá đại diện từ tập variant ứng viên, sắp xếp theo khóa cho phép rồi mới phân trang. Đọc thêm một dòng để tính `hasNext`. Tránh phân trang trên các dòng variant trước khi gom.
3. Project DTO với query read-only. Phân tích kế hoạch truy vấn trên dữ liệu đại diện, đặc biệt category root và bộ lọc kết hợp. Tìm tên chứa từ khóa có thể cần quét dữ liệu. Nâng cấp tìm kiếm theo số đo thay vì giả định index thường sẽ giải quyết LIKE có wildcard đầu.
4. Public GET có thể cache ở edge. Đề xuất TTL 60 giây, key gồm route và query đã chuẩn hóa. Chỉ cache 200 public. Detail/category có ETag riêng nếu triển khai conditional GET. Khi bật cache, purge chi tiết, category hoặc nhóm cache list bị ảnh hưởng sau commit và theo dõi độ trễ purge. Admin và nội bộ dùng `Cache-Control: no-store`.

## Luồng ghi và nhất quán

- Admin gửi `If-Match`. Trong transaction, lấy khóa theo thứ tự thống nhất **category theo ID → product theo ID → rows con** nếu các bản ghi đó tham gia invariant. Khi kiểm Published, khóa cả category và root liên quan. Khi đổi category của product, khóa cả đường dẫn cũ và mới. Sau khi khóa, đọc lại trạng thái và version. Nếu bộ khóa cần dùng đã đổi vì giao dịch đồng thời, chạy lại việc xác định bộ khóa trong transaction mới.
- Cập nhật version của aggregate và giữ unique constraints trên DB. Lỗi trùng khóa được ánh xạ theo constraint. Version cũ trả 412, kể cả variant hay gallery đã thay đổi giữa hai lần đọc.
- Lưu thay đổi, audit và outbox trong cùng transaction. Outbox publisher chạy sau commit, retry có backoff và dùng event ID ổn định. Nếu gửi thành công rồi publisher restart trước khi ghi nhận, consumer dùng event ID để bỏ qua bản trùng.
- Đề xuất một event `catalog.product.changed.v1` với `eventId, occurredAt, productId, productVersion, changeType, affectedSkus, correlationId` và `catalog.category.changed.v1` tương ứng cho category. Không phụ thuộc event để xác thực giá checkout. Consumer áp dụng version mới hơn cho projection và thực hiện purge lặp an toàn.
- Admin POST tạo category/product/variant dùng unique key để ngăn tạo trùng khi retry. Khi timeout, client đọc lại theo slug hoặc SKU qua danh sách Admin trước khi thử lại. Upload timeout có thể tạo phiên khác. Job dọn phiên cũ xử lý upload bỏ dở.

## Ảnh sản phẩm

Admin tạo upload session riêng cho product. Upload vào vùng staging private bằng URL có hạn. Đề xuất JPEG, PNG, WebP, tối đa 5 MiB/ảnh, 10 ảnh/product. Worker xác minh kích thước thực, nhận diện và giải mã ảnh, tạo đối tượng ảnh cuối có key bất biến và đánh dấu Ready. Từ chối upload sai format, quá giới hạn hoặc hết hạn. Toàn bộ kiểm tra áp dụng cho dữ liệu nhận thực tế.

`PUT /images` chỉ gắn ảnh Ready thuộc product đang sửa, theo đúng version của product. Gallery có thứ tự và ảnh đầu làm thumbnail. URL public sinh từ asset key đã xuất bản. Giữ staging và ảnh draft ngoài bề mặt đọc công khai. Khi ngừng bán, gỡ tham chiếu public và purge cache theo chính sách CDN. Dọn upload hết hạn và asset mồ côi sau khoảng thời gian giữ cấu hình được, sau khi xác minh không còn tham chiếu.

## Ranh giới tích hợp

| Service | Hợp đồng |
|---|---|
| Ocelot/Storefront | Route public và Admin. Web ghép kết quả catalog với availability từ Inventory để hiển thị theo SKU. |
| Basket | Resolve SKU để từ chối variant ngừng bán. Kiểm tra tình trạng hàng qua Inventory theo luồng giỏ. |
| Ordering | Resolve lại SKU khi tạo checkout, lưu giá USD và metadata đóng gói vào snapshot. Dùng Inventory để giữ toàn bộ SKU. |
| Inventory | Khóa liên kết là SKU bất biến. Quy trình đề xuất tạo stock record khi Admin nhập tồn lần đầu. SKU chưa có stock record được xử lý như không có hàng. |
| Shipping | Dùng trọng lượng và kích thước từ snapshot checkout. Quyết định quy cách kiện và cước nằm trong Shipping. |

Không ghi chéo database. Thiếu hoặc lỗi availability không đổi trạng thái catalog. Ordering phải xử lý SKU ngừng bán sau khi báo giá theo chính sách checkout. Đề xuất kiểm lại ngay trước khi mở phiên thanh toán, sau đó xử lý theo snapshot và quy tắc hủy/hoàn đã được duyệt.
