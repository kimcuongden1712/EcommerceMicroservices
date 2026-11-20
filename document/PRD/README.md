# PRD — Ecommerce Microservices

**Phiên bản 0.1 · 20/11/2026 · Bản dự thảo để triển khai và nghiệm thu**

Website bán lẻ thời trang cho một nhà bán, một kho tại California và quản lý tồn theo SKU. Mốc mở bán mục tiêu là **01/01/2027**, dùng **USD** và giao tại **Mỹ**. Giao quốc tế thuộc lộ trình sau ra mắt. Ngân sách và nhân lực không đặt trần trong kế hoạch.

| Phần | Nội dung |
|---|---|
| [01 — Phạm vi](01-scope.md) | Mục tiêu, vai trò, phạm vi và truy xuất BRD |
| [02 — Storefront và tài khoản](02-storefront.md) | Catalog, SKU, tìm kiếm, đăng nhập và lịch sử |
| [03 — Giỏ hàng, checkout và tồn kho](03-checkout-inventory.md) | Quy tắc giá, giữ tồn, đồng thời và phục hồi |
| [04 — Thanh toán và vòng đời đơn](04-payment-orders.md) | Stripe, trạng thái, hủy, hoàn tiền và đối soát |
| [05 — Giao hàng và thuế](05-shipping-tax.md) | FedEx, vùng giao, báo giá, nhãn và tracking |
| [06 — Quản trị và vận hành](06-admin-operations.md) | Quyền, nhập hàng, hỗ trợ và nhật ký |
| [07 — Kiến trúc và dữ liệu](07-architecture.md) | Ranh giới service, TEDU, dữ liệu và tích hợp |
| [08 — Yêu cầu phi chức năng và tải](08-nfr-load.md) | Hiệu năng, bảo mật, khả năng phục hồi và đo tải |
| [09 — Nghiệm thu và phát hành](09-release.md) | Tiến độ, kịch bản UAT, go/no-go và rollback |
| [10 — Sổ quyết định](10-decisions.md) | Quyết định đã chốt và các mục cần xác nhận |

**Cách bảo trì.** Giữ mã yêu cầu ổn định khi đổi tên hoặc di chuyển nội dung. Cập nhật quyết định tại mục 10 và sửa đúng phần sở hữu yêu cầu. Điều chỉnh ca UAT liên quan khi đổi hành vi. Các mục đánh dấu **đề xuất** dùng để lập kế hoạch. Các mục **mở** có chủ sở hữu và thời điểm cần chốt.

**Đầu vào.** [BRD ban đầu](../../resources/BRD_Ecommerce.pdf) và [sơ đồ kiến trúc](../../resources/Microservices-Solution_Architect.jpg) nằm trong thư mục resources của workspace. Source tham khảo nằm tại `D:\2.Git\tedu-aspnetcore-microservices-training`.
