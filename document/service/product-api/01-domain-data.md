# 01 — Domain và dữ liệu

[Chỉ mục Product API](README.md)

## Mô hình

Dùng MySQL riêng cho Product. `id` là BIGINT dương sinh tại DB, truyền ra JSON dưới dạng chuỗi để giữ độ chính xác. Thời gian UTC. Tên bảng và cột dưới đây là schema đề xuất.

| Bảng | Cột chính | Ràng buộc và chỉ mục |
|---|---|---|
| `categories` | id, parent_id nullable, name(120), slug(120), status, sort_order, version, created_at, updated_at | FK parent cùng bảng, RESTRICT. UNIQUE slug. Index (parent_id, status, sort_order, id). |
| `products` | id, category_id, name(160), slug(160), description(10000), status, published_at nullable, version, created_at, updated_at | FK category RESTRICT. UNIQUE slug. Index (category_id, status, published_at, id), (status, published_at, id). |
| `product_variants` | id, product_id, sku(64), color_code(48), color_label(80), size_code(32), size_label(80), price_minor, status, weight_g, length_mm, width_mm, height_mm | FK product RESTRICT. UNIQUE sku toàn catalog. UNIQUE (product_id, color_code, size_code). Index (product_id, status, price_minor, id). |
| `product_images` | id, product_id, asset_key(255), alt_text(160), position | FK product RESTRICT. UNIQUE (product_id, position), UNIQUE asset_key. Position 0 là ảnh chính. |
| `product_image_uploads` | id, product_id, staging_key, asset_key nullable, expected_content_type, expected_size, status, expires_at, created_at | FK product RESTRICT. UNIQUE staging_key. Index (status, expires_at). |
| `catalog_audit` | id, entity_type, entity_id, action, actor_id, correlation_id, changed_fields_json, occurred_at | Index (entity_type, entity_id, occurred_at, id). Ghi trong cùng transaction với thay đổi. |
| `catalog_outbox` | id, topic, payload_json, attempts, next_attempt_at, published_at nullable, occurred_at | Dùng cho sự kiện thay đổi catalog để purge cache và tích hợp được duyệt. Index (published_at, next_attempt_at, id). |

Tiền là số nguyên cents, `currency=USD` cố định trong v1. Đề xuất miền giá 1–99.999.999 cents. Kích thước và trọng lượng là số nguyên dương, mang ý nghĩa **thông số một đơn vị SKU khi đóng gói**. Shipping tính kiện vận chuyển cuối từ các thông số này. Draft có thể thiếu giá, kích thước hoặc ảnh. Cột variant liên quan cho phép null khi Draft, còn lệnh kích hoạt kiểm đầy đủ. Admin input và DB cùng bảo vệ dữ liệu đã chuẩn hóa.

## Khóa và quy tắc

- Slug là chữ thường ASCII, số và dấu gạch ngang. SKU chuẩn hóa uppercase, cho phép chữ, số, gạch ngang và gạch dưới, 1–64 ký tự. Color/size code được chuẩn hóa riêng, label dùng để hiển thị. So sánh khóa chuẩn hóa bằng collation phân biệt theo byte hoặc tương đương.
- Slug sản phẩm ổn định sau lần công bố đầu tiên. Slug category ổn định sau lần kích hoạt đầu tiên. SKU và cặp màu × size cố định sau khi variant được tạo. Muốn thay định danh thì tạo variant mới và ngừng bán variant cũ. Không tái sử dụng SKU đã retire.
- Danh mục có tối đa hai cấp. Parent của danh mục con phải là root. Parent không đổi sau khi tạo. Sản phẩm gắn một danh mục; lọc theo root bao gồm cả sản phẩm gắn trực tiếp và sản phẩm thuộc các con của root.
- Product, variant và category đang phục vụ tham chiếu nghiệp vụ dùng ngừng bán hoặc vô hiệu hóa. Giữ ID để truy xuất; đơn lịch sử đọc snapshot từ Ordering.
- Product public khi Published, category và parent nếu có đang Active, và có ít nhất một variant Active. Variant Active thể hiện **được phép bán theo catalog**, còn còn hàng hay hết hàng do Inventory trả về.

## Vòng đời và điều kiện chuyển

| Đối tượng | Chuyển trạng thái | Điều kiện |
|---|---|---|
| Category | Active ↔ Inactive | Kích hoạt con cần parent Active. Vô hiệu hóa bị từ chối nếu có con Active hoặc sản phẩm Published gắn trực tiếp. |
| Product | Draft → Published | Tên, slug, mô tả, category hiệu lực, ảnh chính Ready và ít nhất một variant Active hợp lệ. |
| Product | Published → Unpublished → Published | Ngừng bán rút khỏi catalog. Khi công bố lại áp dụng lại toàn bộ điều kiện công bố. |
| Variant | Draft → Active → Retired | Kích hoạt cần giá và thông số đóng gói hợp lệ. Retired là trạng thái cuối. Từ chối retire variant Active cuối hoặc bỏ ảnh chính nếu product đang Published và thay đổi làm mất điều kiện công bố. |

`version` của product tăng cho mọi thay đổi của product, variant hoặc ảnh. Category có version độc lập. Khi cập nhật hoặc di chuyển product đang Published, kiểm lại category đích và các invariant của trạng thái Published. Đề xuất hiển thị giá thấp nhất trong các SKU Active phù hợp với bộ lọc. Khi đồng thời lọc màu và size, **cùng một SKU** phải thỏa cả hai.
