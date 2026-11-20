# Product API — Thiết kế chi tiết

**Phiên bản 0.1 · 20/11/2026 · Đề xuất để triển khai**

Product API sở hữu danh mục thời trang, nội dung sản phẩm, ảnh, giá USD, biến thể màu × size và thông số đóng gói. Storefront đọc catalog qua Ocelot. Admin chỉnh sửa, công bố và ngừng bán. Basket và Ordering đọc SKU từ Product khi xác thực giỏ và chốt giá. Inventory sở hữu tồn, giữ hàng và khả năng cấp hàng tại thời điểm mua.

| Tài liệu | Nội dung |
|---|---|
| [01 — Domain và dữ liệu](01-domain-data.md) | Entity, schema MySQL, chỉ mục, vòng đời và invariant |
| [02 — API contract](02-api-contract.md) | Public, Admin, internal, DTO, hợp đồng lỗi và phân quyền |
| [03 — Xử lý và tích hợp](03-processing-integrations.md) | Kiến trúc code, transaction, cache, ảnh và ranh giới service |
| [04 — Triển khai và nghiệm thu](04-delivery-testing.md) | Kế hoạch tái sử dụng TEDU, vận hành, test và quyết định mở |

## Mục tiêu và truy xuất yêu cầu

| Nguồn | Phần thiết kế |
|---|---|
| CAT-01 | Danh mục hai cấp, tìm theo từ khóa, lọc màu/size, giá, phân trang |
| CAT-02–03 | Chi tiết bằng slug ổn định, hình ảnh, SKU, giá và khả năng bán |
| ADM-01–02 | Quản trị danh mục/sản phẩm/SKU, lưu nháp, công bố, ngừng bán, dữ liệu đóng gói |
| CHK-01–02, INV-01 | Tra cứu SKU và giá hiện tại cho checkout. Ordering lưu snapshot, Inventory giữ hàng. |
| NFR-01–07 | Phân quyền, đồng thời, đo tải, backup và khả năng vận hành |

Các mặc định như danh mục hai cấp, một danh mục mỗi sản phẩm, giới hạn phân trang và vòng đời catalog là **quyết định thiết kế đề xuất**. Thuế, vận đơn, tổng phí giao và tồn kho lấy từ các thành phần sở hữu tương ứng.

[Chỉ mục service](../README.md) · [PRD storefront](../../PRD/02-storefront.md) · [PRD quản trị](../../PRD/06-admin-operations.md) · [PRD kiến trúc](../../PRD/07-architecture.md)
