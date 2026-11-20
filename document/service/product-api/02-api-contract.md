# 02 — API contract

[Chỉ mục Product API](README.md) · [Domain](01-domain-data.md)

Prefix công khai và Admin là `/api/v1`, JSON camelCase. Prefix internal là `/internal/v1` trên mạng nội bộ. Bảng dưới mô tả contract v1 dự kiến.

## Public · Anonymous

| Method và path | Đầu vào | Thành công |
|---|---|---|
| GET /api/v1/categories | Không có | 200. Cây category Active, tối đa hai cấp, sắp xếp theo sortOrder rồi id. |
| GET /api/v1/products | `q`, `categorySlug`, `color`, `size`, `minPriceMinor`, `maxPriceMinor`, `sort`, `page`, `pageSize` | 200. Trang sản phẩm Published với giá đại diện theo bộ lọc. Không có kết quả trả mảng rỗng. |
| GET /api/v1/products/{slug} | Slug ổn định | 200. Chi tiết, gallery và variants Active. 404 nếu không công khai hoặc không tồn tại. |

Mặc định `sort=newest`, `page=1`, `pageSize=24`. Cho phép `priceAsc`, `priceDesc`, `newest`. Giới hạn đề xuất pageSize 100 và offset 10.000 bản ghi. Sort tie-break theo ID. `priceAsc` và `priceDesc` dùng cùng giá đại diện là giá nhỏ nhất của các variant khớp bộ lọc. Query tìm tên hoặc SKU bằng từ khóa trim, tối đa 100 ký tự. Tên tìm không phân biệt hoa thường theo collation đã chọn, SKU tìm chính xác sau chuẩn hóa. Filter category không tồn tại trả trang rỗng. Giá lọc trên từng variant, với min ≤ max. Truy vấn vượt giới hạn hoặc sort ngoài danh sách trả 400.

```json
{
  "items": [
    {
      "id": "101",
      "slug": "classic-tee",
      "name": "Classic Tee",
      "categorySlug": "t-shirts",
      "thumbnail": {"url": "https://images.example.com/catalog/classic-tee.webp", "altText": "Classic Tee in black"},
      "priceFromMinor": 2400,
      "currency": "USD"
    }
  ],
  "page": 1,
  "pageSize": 24,
  "hasNext": false
}
```

`ProductDetail` trả các trường nhận diện, `description`, `images[]` theo thứ tự và `variants[]` với `id, sku, colorCode, colorLabel, sizeCode, sizeLabel, priceMinor, currency`. Trạng thái giao diện **còn hàng** được storefront lấy qua Inventory hoặc lớp tổng hợp của web. Khi Inventory chưa trả lời, storefront hiển thị trạng thái chưa xác định và quyền thêm/mua tiếp tục qua bước xác thực giỏ.

## Admin · Policy CatalogAdmin

| Method và path | Chức năng | Response |
|---|---|---|
| GET /api/v1/admin/categories | Xem danh mục gồm inactive | 200 |
| GET /api/v1/admin/categories/{id} | Chi tiết danh mục và ETag | 200 |
| POST /api/v1/admin/categories | Tạo category: `parentId, name, slug, sortOrder`. Mặc định Inactive. | 201 + Location + ETag |
| PUT /api/v1/admin/categories/{id} | Sửa `name, sortOrder, slug`; giới hạn slug theo vòng đời | 200 + ETag |
| PUT /api/v1/admin/categories/{id}/status | Đổi Active/Inactive | 200 + ETag |
| GET /api/v1/admin/products | Lọc theo q, categoryId, status, page, pageSize | 200, envelope phân trang |
| GET /api/v1/admin/products/{id} | Chi tiết mọi trạng thái, SKU, ảnh, thông số đóng gói | 200 + ETag |
| POST /api/v1/admin/products | Tạo Draft: `name, slug, categoryId, description?` | 201 + Location + ETag |
| PUT /api/v1/admin/products/{id} | Sửa `name, slug, categoryId, description` | 200 + ETag |
| PUT /api/v1/admin/products/{id}/status | Công bố Published hoặc chuyển Unpublished | 200 + ETag |
| POST /api/v1/admin/products/{id}/variants | Tạo Draft: SKU, màu, size, giá và thông số đóng gói tùy trạng thái | 201 + Location + ETag product |
| GET /api/v1/admin/products/{id}/variants/{variantId} | Chi tiết variant, kèm ETag product | 200 |
| PUT /api/v1/admin/products/{id}/variants/{variantId} | Sửa label, giá, trọng lượng và kích thước | 200 + ETag product |
| PUT /api/v1/admin/products/{id}/variants/{variantId}/status | Kích hoạt hoặc retire | 200 + ETag product |
| POST /api/v1/admin/products/{id}/image-uploads | Khởi tạo upload qua storage, nhận `contentType, contentLength` | 201 + Location. Trả uploadId, uploadUrl, headers bắt buộc và thời hạn |
| GET /api/v1/admin/products/{id}/image-uploads/{uploadId} | Xem Pending, Ready, Rejected hoặc Expired | 200 |
| PUT /api/v1/admin/products/{id}/images | Thay gallery có thứ tự bằng `images: [{uploadId, altText}]`. Chấp nhận upload đã gắn vào chính product đó. | 200 + ETag product |

Mọi lệnh sửa aggregate đã tồn tại dùng `If-Match` theo ETag đọc từ GET tương ứng, kể cả tạo variant và thay gallery. Khởi tạo upload chỉ tạo phiên upload nên dùng quyền CatalogAdmin và kiểm tra product tồn tại. Các thao tác trả 201 cho resource có endpoint đọc kèm Location. Description lưu plain text trong MVP. Client không ghi được status qua DTO sửa thông thường.

## Internal · Policy CatalogServiceReader

`POST /internal/v1/catalog/skus:resolve` nhận `{"skus":["TEE-BLK-M","TEE-WHT-S"]}`, tối đa 100 SKU duy nhất sau chuẩn hóa. Trả 200 theo thứ tự request với trạng thái từng SKU `availableForSale`, `notForSale`, `notFound`. SKU bán được trả `productId, variantId, sku, name, colorLabel, sizeLabel, priceMinor, currency, weightG, lengthMm, widthMm, heightMm, productVersion`. Giải nghĩa `availableForSale` chỉ theo catalog. Endpoint đọc nguồn chính hoặc bản đọc đã chứng minh đáp ứng tính nhất quán checkout. Không CDN cache route này.

Ordering dùng giá và dữ liệu đóng gói để xây quote và snapshot. Nếu đối chiếu với giỏ khác giá hoặc SKU ngừng bán, yêu cầu Buyer xác nhận lại giỏ trước khi trả tiền. Nếu Product timeout, Ordering dừng checkout và cho thử lại.

## Lỗi, version và quyền

Dùng `application/problem+json` với `type, title, status, code, traceId` và `errors` cho validation. Ví dụ mã lỗi `SKU_ALREADY_EXISTS`, `SLUG_ALREADY_EXISTS`, `CATEGORY_HAS_ACTIVE_DEPENDENTS`, `PUBLISH_REQUIREMENTS_NOT_MET`, `VERSION_MISMATCH`.

| HTTP | Khi dùng |
|---|---|
| 400 | Payload, query, mã SKU hoặc yêu cầu validation sai |
| 401 / 403 | Chưa xác thực / token hợp lệ nhưng thiếu quyền |
| 404 | Không tồn tại, variant không thuộc product hoặc public resource không công khai |
| 409 | Trùng SKU/slug hoặc thao tác vi phạm vòng đời, invariant |
| 412 / 428 | If-Match không còn khớp / thiếu If-Match bắt buộc |
| 429 / 503 | Vượt rate limit / phụ thuộc tạm thời không khả dụng |

Public GET cho anonymous. Admin yêu cầu token đúng audience và CatalogAdmin. Internal chỉ cấp cho danh tính workload Basket/Ordering theo scope đọc catalog, không route Internet. Phân quyền tại Product API kể cả request đi qua gateway.
