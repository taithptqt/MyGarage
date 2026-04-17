# UAT Checklist - Luồng Báo Giá Bảo Hiểm E2E

Ngày cập nhật: 2026-04-16  
Phạm vi trạng thái: `DRAFT -> PENDING_INSURANCE -> INSURANCE_APPROVED -> PENDING_CUSTOMER -> CUSTOMER_APPROVED`

## 1. Vai trò tham gia
- Lễ tân/CVDV
- Giám định viên bảo hiểm (off-system)
- Khách hàng chủ xe
- Quản đốc/Tổ trưởng
- Thủ kho
- Kế toán

## 2. Dữ liệu đầu vào tối thiểu
- Có `ServiceOrder` đang mở, có `ServiceAdvisorId` hoặc `ReceptionistId`.
- Có `InsuranceCompany` đang hoạt động.
- Có `Quotation` gồm cả công và vật tư, tổng tiền > 0.
- Có SMTP nếu test gửi OTP qua email.

## 3. Checklist theo luồng nghiệp vụ

### A. Lập báo giá và đàm phán off-system
1. Tạo báo giá V1 từ màn hình nghiệp vụ.
2. Xuất file qua `GET /api/quotations/{quotationId}/export/excel`.
3. Kiểm tra file chỉ có 2 cột:
   - Tên hạng mục
   - Đơn giá xưởng đề xuất

Kết quả mong đợi:
- Xuất file thành công.
- Hiển thị tiếng Việt không lỗi font.

### B. Nhập kết quả đàm phán và nộp bằng chứng
1. CVDV cập nhật split billing từng dòng.
2. Chuyển báo giá về `PENDING_INSURANCE`.
3. Upload bằng chứng qua `POST /api/quotations/{quotationId}/insurance-evidence`.
4. Xác nhận bảo hiểm duyệt qua `POST /api/quotations/{quotationId}/insurance-approval`.

Kết quả mong đợi:
- Thiếu `insuranceApprovalEvidenceUrl` thì không duyệt được.
- Báo giá chuyển `INSURANCE_APPROVED`.
- Dữ liệu split billing khóa đúng theo quy tắc.

### C. Gửi link cho khách hàng
1. Tạo magic link:
   - `POST /api/quotations/{quotationId}/publish-customer-link` (`sendEmail=false`) để gửi Zalo/SMS.
   - Hoặc `sendEmail=true` nếu gửi email.
2. Endpoint tương thích cũ: `POST /api/quotations/{quotationId}/send-to-customer`.

Kết quả mong đợi:
- Sinh `QuotationAccessLink` trạng thái `ACTIVE`.
- Link active cũ bị `REVOKED`.
- Báo giá chuyển `PENDING_CUSTOMER`.

### D. Khách mở link và ký duyệt bằng OTP
1. Xem báo giá: `GET /api/quotations/public/{token}`.
2. Gửi OTP: `POST /api/quotations/public/{token}/otp/send`.
3. Duyệt/từ chối: `POST /api/quotations/public/{token}/decision`.

Kết quả mong đợi:
- OTP sai quá số lần thì bị chặn.
- OTP đúng thì quyết định được ghi nhận.
- Nếu duyệt:
  - Báo giá chuyển `CUSTOMER_APPROVED`.
  - Trigger thông báo nội bộ đúng vai trò.

### E. Trigger vận hành nội bộ sau khi khách duyệt
1. CVDV nhận thông báo khách đã duyệt.
2. Quản đốc/Tổ trưởng nhận tín hiệu sẵn sàng thi công.
3. Thủ kho nhận tín hiệu chuẩn bị vật tư.

Kết quả mong đợi:
- Không thiếu thông báo.
- Không sai trạng thái liên thông.

### F. Đóng băng dữ liệu và sẵn sàng tách bill
1. Kiểm tra cờ sẵn sàng tách bill sau khi duyệt.
2. Thử sửa split billing sau khi đã khóa.

Kết quả mong đợi:
- Không sửa được khi báo giá đã khóa.
- Tổng bảo hiểm/khách luôn khớp với dòng chi tiết.

## 4. Checklist kỹ thuật
- Constraint bắt buộc:
  - `CK_Quotations_InsuranceApprovalEvidence_Required`
  - `CK_QuotationDetails_CoverageSum`
- Migration đã áp dụng:
  - `20260416093000_DropLegacyQuotationDetailCoverageColumns`
- Xác nhận `QuotationDetails` chỉ còn cột chuẩn:
  - `InsuranceCoveredAmount`
  - `CustomerCoveredAmount`

## 5. Tiêu chí Pass UAT
- Pass toàn bộ kịch bản A -> F.
- Không có lỗi chặn luồng nghiệp vụ.
- Không lỗi encoding tiếng Việt có dấu.
- Không có lỗi exception mức critical trong log.
