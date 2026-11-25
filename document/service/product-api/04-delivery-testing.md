# 04 — Triển khai và nghiệm thu

## English

This document describes the Product.API delivery and acceptance approach. The source-derived image upload, processing, gallery and private delivery behavior is documented in [Product Image Architecture](05-product-images.md). The acceptance table below includes catalog, concurrency, authorization, integration, performance, and media regression scenarios. Run unit tests for domain/validation behavior, database integration tests for constraints and concurrency, and HTTP/contract tests for API and service boundaries. Mark unavailable Garage/browser end-to-end checks as `SKIPPED`, never as passed.

Direct browser upload E2E must wait until the real MFE origin is known and the relevant CORS policy has been explicitly approved. Garage staging CORS and API/gateway CORS are separate controls. Do not use wildcard origins, browser Garage credentials, or log presigned URL query strings. Operational guidance and open product decisions remain proposals where identified below; they are not claims of production readiness.

## Tiếng Việt

[Chỉ mục Product API](README.md) · [PRD nghiệm thu](../../PRD/09-release.md)

## Tái sử dụng TEDU

Source TEDU đã kiểm tra có `Product.API.csproj` target .NET 10, controller CRUD, `CatalogProduct`, `ProductContext`, repository, migration, health checks và Dockerfile. Dùng bố cục ASP.NET Core, cấu hình EF/MySQL và các phần đóng gói phù hợp sau khi build và chạy thử.

- Tạo model và migration riêng cho category, product, variant, ảnh, upload, audit và outbox. Không áp migration TEDU vào database production của dự án. Data mẫu hoặc dữ liệu import phải chạy theo quy trình riêng, có báo cáo SKU/slug trùng và schema validation.
- Tách public/Admin/internal. Controller TEDU có `[AllowAnonymous]` ở cấp class, vì vậy thiết kế mới đặt quyền anonymous chỉ ở các endpoint public cần thiết. Kiểm thử policy tại Product API.
- Đổi truy vấn list sang projection theo SKU và danh sách sort cho phép. Thay hợp đồng pagination và route mẫu bằng contract v1 sau khi thống nhất với Ocelot, Storefront, Basket và Ordering.
- Chọn phiên bản SDK, EF và provider MySQL dựa trên build/restore và test trên MySQL thật. Xác nhận quyền vận hành công cụ và image đi kèm trước production.

## Thứ tự giao

| Đợt | Hạng mục | Điều kiện hoàn thành |
|---|---|---|
| 1 | Project, DB, migrations, model, seed dev và health checks | Build sạch. Có thể tạo DB mới từ migration. Seed chạy lại an toàn. |
| 2 | Admin category/product/variant và policy | CRUD theo vòng đời, unique constraints, If-Match, audit và khoá race. |
| 3 | Ảnh và công bố | Upload private, xác minh, gallery, publish và unpublish hoạt động. |
| 4 | Public catalog và internal resolve | Filter/sort/page đúng, response ổn định, Basket/Ordering dùng được contract. |
| 5 | Gateway, cache/outbox và vận hành | Gateway không công khai internal, event retry được, có số đo truy vấn và backup/restore. |

## Nghiệm thu kỹ thuật

| Ca | Kiểm chứng |
|---|---|
| PAPI-01 | Draft/Unpublished, category Inactive và variant Retired không xuất hiện trên public. Item không hợp lệ trả 404 đúng nơi. |
| PAPI-02 | Danh mục hai cấp, lọc root/con, từ khóa, cùng-SKU màu×size, giá lọc, sorting tie-break và trang rỗng. |
| PAPI-03 | SKU trùng khác chữ hoa, slug trùng, màu×size trùng và tạo đồng thời được ràng buộc DB. |
| PAPI-04 | Không publish thiếu ảnh Ready hoặc variant đủ điều kiện. Retire variant cuối và deactivate category cùng lúc với publish không phá invariant. |
| PAPI-05 | If-Match cũ trả 412, thiếu trả 428. Hai Admin sửa đồng thời không ghi đè. Audit khớp tác nhân. |
| PAPI-06 | Upload hết hạn, sai dữ liệu, ảnh thuộc product khác, đổi thứ tự gallery và cleanup sau khi gỡ ảnh. |
| PAPI-11 | `GET /api/v1/media/{assetKey}` chỉ trả ảnh được tham chiếu bởi product Published; kiểm tra draft/unpublished, key staging, key sai định dạng, metadata lệch và Garage tạm lỗi. |
| PAPI-07 | Anonymous/Buyer không ghi Admin. Token nội bộ sai scope bị từ chối. API Gateway không route internal ra Internet. |
| PAPI-08 | Batch resolve giữ thứ tự, phân biệt SKU không bán và không tồn tại, thấy giá hiện hành. Checkout xử lý Product timeout hoặc giá thay đổi. |
| PAPI-09 | DB commit nhưng broker lỗi, publisher restart sau khi publish và consumer nhận trùng. Catalog vẫn giữ dữ liệu đúng và purge có thể chạy lại. |
| PAPI-10 | Đo p95, error rate và kế hoạch query trên dữ liệu đại diện. Kiểm tra cache nóng/lạnh, độ trễ sau unpublish và diễn tập restore. |

Dùng unit test cho quy tắc vòng đời và validation. Dùng integration test với MySQL và test HTTP cho unique, transaction, concurrency và authz. Dùng contract test với Gateway, Basket, Ordering. Ngưỡng hiệu năng định lượng dùng hồ sơ tải đã duyệt trong PRD. Giả định 100 triệu đồng thời thuộc thử tải cấp hệ thống.

### Direct upload E2E sau khi MFE được triển khai

Chỉ chạy các bước CORS sau khi đã ghi nhận origin chính xác cho environment đang kiểm thử. Không thay thế origin bằng `*`.

1. Xác nhận Product API đang trả `uploadUrl` có thể truy cập từ browser và `headers.Content-Type` đúng MIME type của file. Không log URL query string.
2. Từ đúng origin của MFE, gọi `POST /api/v1/admin/products/{productId}/image-uploads` với `contentType` và `contentLength`; kiểm tra `201`, `uploadId`, `uploadUrl`, `headers` và `expiresAt`.
3. Nếu browser phát sinh preflight, kiểm tra `OPTIONS` tới host Garage trả CORS cho đúng `Origin`, method `PUT` và request header `Content-Type`. Không dùng credentials của Garage trong browser.
4. Gửi `PUT` trực tiếp tới `uploadUrl`, body là raw bytes của file, và chỉ đặt `Content-Type` theo đúng giá trị trong response. Không gửi JSON, multipart hoặc `Authorization`.
5. Kiểm tra `PUT` thành công, sau đó chờ worker và gọi `GET /api/v1/admin/products/{productId}/image-uploads/{uploadId}` cho tới `Ready`. Kiểm tra MIME type, kích thước và nội dung ảnh được xác minh ở server.
6. Gọi `PUT /api/v1/admin/products/{productId}/images` với `If-Match` hiện tại để gắn upload vào gallery; publish product khi đủ invariant.
7. Gọi public catalog và kiểm tra URL ảnh là `/api/v1/media/{assetKey}`. Từ đúng storefront origin, tải URL đó và xác nhận bytes/MIME type; staging URL và asset chưa được tham chiếu phải không truy cập được.
8. Lặp lại với sai MIME type, sai kích thước, URL hết hạn, object staging bị xóa trước worker, origin không nằm trong allowlist và thay đổi gallery. Mỗi ca phải ghi rõ HTTP status, upload status và việc staging/object cleanup; test bị bỏ qua phải ghi `SKIPPED`, không tính là `PASS`.

## Vận hành

Tách liveness khỏi readiness. Readiness kiểm MySQL. Theo dõi lỗi storage, backlog/tuổi outbox, tỷ lệ lỗi publish, p95 theo endpoint, truy vấn chậm, số 409/412 và độ trễ cache purge. Gắn correlation ID theo request và event. Quản lý connection string, credential storage và token theo môi trường. Chạy migration như bước phát hành riêng. Khi rollback ứng dụng, giữ schema tương thích và đối chiếu catalog/cache trước khi mở lại Admin write.

## Quyết định cần xác nhận

| Mục | Đề xuất |
|---|---|
| Taxonomy | Hai cấp, một category mỗi product, mã màu/size chuẩn hóa và label hiển thị. |
| Quản lý định danh | SKU bất biến khi tạo, slug bất biến kể từ công bố/kích hoạt. |
| Media | Object storage + CDN. Cần chọn nhà cung cấp, chính sách thời hạn URL và retention. |
| Danh tính | Chốt Identity backend, policy Admin, token workload và audience trước tích hợp gateway. |
| Kích thước catalog | Xác nhận số sản phẩm, SKU tối đa mỗi product, quy tắc SKU và SLA cập nhật cache trước kiểm thử tải. |
| Pricing | Giá độc lập theo variant, USD cents. Chốt giới hạn giá và cách hiển thị giá khi có bộ lọc. |
| Hàng tồn | Admin nhập tồn trong Inventory. Chốt route đọc availability theo batch và cách hiển thị khi Inventory tạm thời lỗi. |
