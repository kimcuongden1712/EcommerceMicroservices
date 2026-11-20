# 08 — Yêu cầu phi chức năng và tải

[Chỉ mục](README.md) · [Nghiệm thu](09-release.md)

| ID | Yêu cầu | Bằng chứng nghiệm thu |
|---|---|---|
| NFR-01 | Định lượng tải mở bán theo phiên hoạt động, RPS, checkout/giây, tỷ lệ cache, đợt cao điểm và phân bố địa lý. | Báo cáo công suất trên cấu hình production dự kiến, với ngưỡng p95, tỷ lệ lỗi và thời lượng đã duyệt. |
| NFR-02 | Giữ đúng tiền và tồn qua retry, restart, timeout và các yêu cầu đồng thời. | Không bán vượt tồn, thu tiền hai lần, hoàn tiền lặp hoặc tạo nhãn trùng trong các ca UAT. |
| NFR-03 | Bảo vệ phiên, dữ liệu và quyền truy cập. | HTTPS, authz ở cấp tài nguyên, MFA Admin, secret tách môi trường, kiểm tra webhook và rà log không lộ dữ liệu nhạy cảm. Có test Buyer truy cập chéo và test Buyer gọi Admin. |
| NFR-04 | Theo dõi sự cố và khả năng phục hồi. | Dashboard kỹ thuật đo lỗi web, checkout, độ trễ xử lý webhook, hàng đợi, giữ tồn quá hạn và lỗi vận đơn. Mỗi ca đối soát truy được theo mã đơn. |
| NFR-05 | Sao lưu và khôi phục thử. | Có bản ghi diễn tập restore database quan trọng, thời gian thực đo và kết quả đối soát. RPO/RTO cần được phê duyệt tại mục 10. |
| NFR-06 | Duy trì trải nghiệm khi tích hợp lỗi. | Catalog vẫn duyệt được khi hãng vận chuyển lỗi. Checkout không tiến hành khi tổng tiền hoặc giao hàng chưa xác định. Lỗi được hướng dẫn thử lại và có mã truy vết. |
| NFR-07 | Trải nghiệm responsive và dễ tiếp cận. | Duyệt danh mục và hoàn tất checkout trên mobile/desktop, thao tác bàn phím, có nhãn trường và thông báo lỗi dễ hiểu. |

## Giả định 100 triệu người dùng đồng thời

Giữ 100 triệu người dùng đồng thời làm giả định thiết kế và mô phỏng toàn hệ thống. Ví dụ, một yêu cầu mỗi 30 giây trên 100 triệu phiên tương đương khoảng 3,33 triệu request/giây trước cache. Đây là mô hình minh họa. Công suất mở bán ngày 01/01 có hồ sơ tải và bộ ngưỡng nghiệm thu riêng do Product và SRE phê duyệt.

- **Đọc catalog.** Đo cache nóng/lạnh, traffic theo vùng, tỷ lệ hit CDN và tải thực tới origin.
- **Giỏ và checkout.** Tách RPS ghi giỏ, tỷ lệ chuyển đổi sang checkout, webhook, giữ tồn và backlog sau khi tải giảm.
- **SKU bán nhanh.** Tập trung yêu cầu vào một SKU, đo contention và kiểm tính đúng của tồn.
- **Đối tác và sự cố.** Dùng mô phỏng Stripe/FedEx cho stress test và mô phỏng timeout, retry, mất kết nối. Kiểm thử tích hợp nhà cung cấp riêng bằng tài khoản test được phép.
- **Thứ tự thử.** Baseline → tăng từng bậc → spike → soak → failover. Dừng khi chạm ngưỡng bảo vệ của môi trường thử hoặc phát hiện lỗi tính đúng dữ liệu.
