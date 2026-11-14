# Module 6 — bằng chứng và việc chưa đóng

Trạng thái: **đang thực hiện, chưa nghiệm thu**. Tài liệu này chỉ ghi các lát cắt đã kiểm tra, chưa phải kiểm kê đầy đủ route, caller hoặc màn hình của module.

| Lát cắt | Bằng chứng | Giới hạn / việc tiếp theo |
|---|---|---|
| Phiếu trả và dòng trả | [CODE] Kiểm tra phạm vi chi nhánh, trạng thái, quan hệ đơn và kho; chặn số hoàn vượt giá trị đơn còn lại trước khi ghi; không nhận `UpdatedBy` từ client khi tạo; không tự cắt `Invoice.PaidAmount` khi không có giao dịch thanh toán. [TEST] `RetailReturnScopeTests` kiểm tra HTTP, dấu vết người tạo và hóa đơn trong cơ sở dữ liệu InMemory. | [BUSINESS/OPEN] Chưa xác nhận cách phân bổ tiền hoàn theo giá, thuế, chiết khấu và thanh toán; giới hạn tổng không chứng minh giá trị hoàn đúng. Nhập kho, trạng thái hàng lỗi, giao dịch hoàn tiền và đối soát hóa đơn chưa được nghiệm thu. Các thao tác CRUD và hoàn tất cần kiểm thử đồng thời với schema thật. |
| Trừ kho bán lẻ | [CODE] Có khóa ứng dụng SQL theo chi nhánh trong `RetailOrderStockCommitter`; [TEST] LocalDB kiểm tra xử lý cùng đơn và hai đơn cùng tranh chấp tồn. | [OPEN] Khóa chưa điều phối các nguồn ghi kho khác; cần kiểm thử retry/commit không rõ kết quả, giao dịch ngoài và schema không điều chỉnh. Không suy ra an toàn tồn kho toàn hệ thống. |
| Voucher và campaign | [CODE] Có phân trang server ở nhiều tab; form tạo campaign bỏ qua phản hồi chi tiết cũ đang chạy. | [OPEN] Cần kiểm kê từng bộ lọc, báo cáo/export và caller; sửa membership campaign vẫn tải toàn bộ ID và cần phương án đồng thời/tương thích trước khi chuyển sang cập nhật từng phần. Kiểm thử đồng thời voucher hiện dùng schema thử đã điều chỉnh. |
| Nhập hàng (phụ thuộc module 5) | [CODE] Đường nhận hàng có transaction và khóa SQL; [TEST] LocalDB có ca cạnh tranh nhận hàng. | [OPEN] Client cũ không có khóa idempotency ổn định qua request; chưa chứng minh mọi luồng ghi kho cùng tham gia cơ chế đồng bộ. |

Kiểm thử LocalDB trong các ca đồng thời hiện dùng script tạo schema đã bỏ một index và các khóa ngoại legacy: không thay thế kiểm thử schema/migration nguyên trạng. Chưa có bằng chứng nghiệm thu production, benchmark trên dữ liệu đại diện hoặc runbook khôi phục cho các lát cắt này.
