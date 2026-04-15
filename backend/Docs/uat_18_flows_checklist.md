# UAT Checklist - 18 Luồng Nghiệp Vụ Ngoài Plan

## A. Luồng nghiệp vụ còn hở (11 luồng)

### 1) 3-way matching mua hàng (PO -> GRN -> Supplier Invoice -> Payment)
- [ ] Tạo supplier invoice với `totalAmount > PO total` bị chặn.
- [ ] Tạo supplier invoice với `totalAmount > GRN total` bị chặn.
- [ ] Tạo supplier invoice detail có `quantity` vượt `orderedQuantity` bị chặn.
- [ ] Tạo supplier invoice detail có `quantity` vượt GRN nhận thực tế bị chặn.
- [ ] Tạo supplier invoice detail có `unitCost` vượt PO unit cost bị chặn.
- [ ] Tạo hợp lệ qua endpoint chuyên dụng thành công.

### 2) Hủy/rollback nghiệp vụ 1 nút
- [ ] Gọi `POST /api/service-orders/{id}/cancel-with-rollback` khi chưa phát sinh thanh toán/hóa đơn => thành công.
- [ ] Kiểm tra material request/material request detail chuyển `CANCELED`.
- [ ] Kiểm tra active quotation link bị `REVOKED`.
- [ ] Kiểm tra bay allocation đang mở được release.
- [ ] Kiểm tra service order chuyển `CANCELED`.
- [ ] Nếu đã có payment COMPLETED hoặc hóa đơn đã phát hành => bị chặn.

### 3) Bảo hành/rework chính thức
- [ ] Gọi `POST /api/service-orders/{id}/warranty-rework` khi có QC FAIL => tạo lệnh REWORK.
- [ ] Nếu không có QC FAIL => bị chặn.
- [ ] Nếu đã có REWORK đang mở => bị chặn tạo trùng.

### 4) Đóng ca thu ngân end-to-end
- [ ] Gọi `POST /api/shift-closings/{id}/reconcile` tính đúng `cash/transfer/online`.
- [ ] `expectedBalance`, `actualBalance`, `discrepancy` tính đúng.
- [ ] Chênh lệch nhỏ (<=1000) => `CLOSED`, lớn hơn => `RECONCILED_WITH_VARIANCE`.

### 5) Đổi/trả bán lẻ đầy đủ
- [ ] Gọi `POST /api/retail-returns/{id}/finalize` thành công khi dữ liệu hợp lệ.
- [ ] Chặn trả quá số lượng đã bán.
- [ ] Sinh `stock_transactions` chiều `IN` đúng kho/sản phẩm.
- [ ] Đồng bộ tổng hoàn và giảm doanh thu order.

### 6) Chuẩn hóa phân quyền (lễ tân kiêm CVDV)
- [ ] Role `RECEPTIONIST` hoặc `SERVICE_ADVISOR` gọi được luồng báo giá bảo hiểm.
- [ ] Role không thuộc nhóm được phép => `403 Forbid`.

### 7) Idempotency cho duyệt/gửi
- [ ] Bấm gửi link khách 2 lần cùng `idempotencyKey` => không tạo side effect lặp.
- [ ] Duyệt bảo hiểm lặp khi đã `INSURANCE_APPROVED` => trả kết quả hiện trạng, không hỏng dữ liệu.

### 8) Outbox + retry + dead-letter notification
- [ ] Mô phỏng lỗi gửi email thông báo => tạo bản ghi `NOTIFY_RETRY_PENDING`.
- [ ] Background service retry gửi lại thành công => `NOTIFY_RETRY_DONE`.
- [ ] Quá số lần retry => `NOTIFY_RETRY_DEAD`.

### 9) Data retention + archive bảng lớn
- [ ] `dbo.sp_archive_operational_logs` tồn tại.
- [ ] Chạy SP thành công trên DB thực tế.
- [ ] Dữ liệu cũ được chuyển sang bảng archive nếu bảng nguồn tồn tại.

### 10) Vehicle history API “1 màn hình”
- [ ] `GET /api/vehicles/{id}/service-history` trả đủ lịch sử dịch vụ/tổng quan.
- [ ] Dữ liệu không rơi thiếu với xe có nhiều service order.

### 11) Runbook vận hành sự cố
- [ ] Tài liệu runbook có RPO/RTO, backup/restore/failover/drill.
- [ ] Team vận hành xác nhận áp dụng được.

## B. Dư thừa/cần hợp nhất (7 mục)

### 12) Trùng controller bay
- [x] Xóa `BaysController.cs`.
- [ ] Regression test route bay admin và bay management.

### 13) Route alias inventory chồng chéo
- [x] Giữ route chuẩn, bỏ alias dư ở InventoryAdmin.
- [ ] Kiểm thử FE đang dùng route cũ.

### 14) Cột split-billing trùng nghĩa
- [x] Dọn các cột legacy ở `QuotationDetail`.
- [ ] Xác nhận migration đã chạy ở tất cả môi trường.

### 15) Trạng thái QC tách đôi
- [x] Đồng bộ `ServiceOrder.Qcstatus` theo `QcInspection.Status`.
- [ ] Kiểm thử đủ nhánh PASS/FAIL/PENDING.

### 16) Claim parse thủ công không thống nhất
- [x] Chuẩn hóa thêm một số controller theo extension.
- [ ] Quét nốt controller còn parse tay ngoài phạm vi hiện tại.

### 17) Quản trị side effect gửi thông báo
- [x] Có queue retry qua audit log + background worker.
- [ ] Theo dõi metric retry/dead sau 7 ngày.

### 18) Chuẩn hóa vận hành sau rollout
- [ ] Chạy smoke-test đầy đủ bằng script.
- [ ] Ký biên bản UAT + checklist production readiness.

## Tài liệu liên quan
- Runbook: [operations_runbook.md](D:/IT/OJT/repos/MyGarage/backend/docs/operations_runbook.md)
- SQL retention: [20260416_data_retention_archive_jobs.sql](D:/IT/OJT/repos/MyGarage/backend/sql/20260416_data_retention_archive_jobs.sql)
