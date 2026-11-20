# 01 — Phạm vi sản phẩm

[Chỉ mục](README.md) · [Sổ quyết định](10-decisions.md)

## Mục tiêu

- Khách có thể tìm và mua đúng biến thể thời trang, biết tổng tiền trước khi thanh toán và theo dõi đơn sau mua.
- Admin có thể duy trì catalog, điều chỉnh tồn, giao hàng và xử lý yêu cầu hủy/hoàn tiền.
- Doanh nghiệp truy vết được mỗi đơn tới khoản thu, biến động tồn và vận đơn. Đo tỷ lệ mua thành công, lỗi checkout, đơn đã trả tiền chờ xử lý và sai lệch đối soát từ ngày đầu.

## Người dùng

| Vai trò | Quyền và mục tiêu |
|---|---|
| Visitor | Duyệt sản phẩm, xem chính sách và chọn biến thể. |
| Buyer | Sau khi đăng nhập, quản lý hồ sơ, địa chỉ, giỏ và đơn của mình. |
| Admin | Quản lý danh mục, SKU, tồn, đơn, vận đơn và hoàn tiền. |
| Kho và hỗ trợ | Tác nhân vận hành. Tài khoản phân quyền riêng chỉ đưa vào bản đầu nếu có nhân sự cần đăng nhập độc lập. |

## Phạm vi 01/01/2027

Một storefront web responsive và một khu vực quản trị. Bán tại Mỹ với USD, một nhà bán, một kho California. SKU theo màu × size. Đăng nhập khi thêm vào giỏ, checkout toàn bộ giỏ, thanh toán trước bằng thẻ qua Stripe theo phương án đề xuất. Một hãng vận chuyển, ứng viên FedEx. Có email giao dịch, tracking theo mốc, chính sách công khai và kênh hỗ trợ.

**Sau ra mắt.** Giao quốc tế, thêm hãng vận chuyển, ví điện tử, giỏ vãng lai, checkout một phần, wishlist, mua lại, đánh giá, Google/Facebook, CMS chỉnh sửa, dashboard kinh doanh, quản lý người mua nâng cao, phân quyền nhân viên, xuất báo cáo, marketing, microfrontend và Flutter. Đặt may theo yêu cầu, COD và tracking vị trí thời gian thực nằm ngoài phạm vi bản đầu.

## Truy xuất BRD

| Nguồn BRD | PRD |
|---|---|
| FR-001–005, FR-010 | Storefront, tài khoản và hồ sơ. Đăng nhập xã hội và hiển thị đánh giá chuyển sau. |
| FR-006, FR-009, FR-011 | Wishlist, chia sẻ chuyên biệt, đánh giá chuyển sau. |
| FR-007–008 | Giỏ và checkout. Thay điều kiện mua một phần bằng toàn bộ giỏ, dùng thẻ trước. |
| FR-012–014 | Lịch sử đơn, tracking, hỗ trợ qua kênh công khai và đăng nhập quản trị. Mua lại chuyển sau. |
| FR-015–019 và mục Payment Management | Quản lý đơn, catalog, thanh toán và vận chuyển trong MVP. Dashboard và quản lý người mua nâng cao chuyển sau. |
| FR-020–026 | Đánh giá quản trị, báo cáo, quản lý nhân viên, CMS động, marketing và quản lý phản hồi chuyển sau. Chính sách tĩnh và kênh hỗ trợ vẫn có. |
| NFR-001–004 | Định nghĩa lại tại mục 08. Giả định 100 triệu đồng thời thay mốc 100 của BRD gốc. |
