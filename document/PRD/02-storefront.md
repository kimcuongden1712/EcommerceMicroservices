# 02 — Storefront và tài khoản

[Chỉ mục](README.md) · [Quản trị catalog](06-admin-operations.md)

| ID | Yêu cầu | Tiêu chí chấp nhận |
|---|---|---|
| CAT-01 | Hiển thị danh mục và danh mục con, phân trang, tìm theo từ khóa, lọc theo danh mục, màu, size và sắp xếp giá. | Khách chưa đăng nhập sử dụng được. Kết quả chỉ chứa sản phẩm công khai và các biến thể phù hợp với bộ lọc. Có thông báo khi không có kết quả. |
| CAT-02 | Trang chi tiết có tên, mô tả, ảnh, giá USD, màu, size và tình trạng bán. | Đổi biến thể cập nhật giá và khả năng thêm hàng theo SKU. Tổ hợp không tồn tại hoặc SKU ngừng bán không thể được chọn để mua. |
| CAT-03 | Trang danh mục và chi tiết hiển thị được trên mobile/desktop, có URL ổn định. | Có thể hoàn tất hành trình bằng bàn phím. URL không tồn tại trả 404 và có đường dẫn quay lại catalog. |
| ACC-01 | Đăng ký email/mật khẩu, đồng ý điều khoản, xác minh email trước khi dùng tài khoản để mua hàng. | Email trùng không tạo tài khoản thứ hai. Liên kết hết hạn có thể yêu cầu gửi lại. |
| ACC-02 | Đăng nhập, đăng xuất, đặt lại mật khẩu và cập nhật tên, số điện thoại, sổ địa chỉ. | Liên kết đặt lại chỉ dùng một lần. Buyer chỉ đọc và sửa hồ sơ của chính mình. |
| ACC-03 | Giữ ý định thêm SKU khi chuyển sang đăng nhập. | Sau đăng nhập, hệ thống quay lại đúng sản phẩm và số lượng, rồi kiểm tra lại tính khả dụng trước khi thêm. |
| ACC-04 | Xem lịch sử đơn, chi tiết tiền hàng, ship, thuế, thanh toán và tracking của chính mình. | Việc chỉnh sửa địa chỉ trong hồ sơ không thay đổi địa chỉ đã lưu trên đơn cũ. |

Giá và tình trạng hàng hiển thị trên catalog có thể cập nhật theo chu kỳ cache. Giá và tồn được xác thực lại ở checkout. Storefront MVP dùng tiếng Anh theo giả định đề xuất cho thị trường Mỹ. Hỗ trợ và các trang điều khoản, bảo mật, giao hàng và đổi trả có thể truy cập trước khi đăng nhập.
