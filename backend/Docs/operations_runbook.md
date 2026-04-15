# Runbook vận hành sự cố MyGarage (Backup/Restore/Failover/DR Drill)

## 1) Mục tiêu
- Đảm bảo có thể khôi phục hệ thống trong sự cố mất dữ liệu hoặc hỏng node.
- Chuẩn hóa thao tác vận hành để không phụ thuộc cá nhân.

## 2) Chỉ số bắt buộc
- `RPO <= 15 phút` (mất dữ liệu tối đa 15 phút).
- `RTO <= 60 phút` (khôi phục dịch vụ tối đa 60 phút).

## 3) Quy trình Backup
- Full backup DB: hằng đêm lúc `01:00`.
- Differential backup: mỗi `4 giờ`.
- Log backup: mỗi `15 phút`.
- Lưu tối thiểu 2 bản: local + offsite (storage khác vùng).
- Mỗi bản backup phải có checksum và log kết quả.

## 4) Quy trình Restore chuẩn
1. Kích hoạt chế độ incident, khóa các tác vụ ghi.
2. Xác định mốc thời gian cần phục hồi.
3. Khôi phục full + diff + log theo mốc.
4. Chạy smoke test API quan trọng:
- Đăng nhập
- Tạo/Sửa lệnh sửa chữa
- Duyệt báo giá bảo hiểm
- Thanh toán
5. Mở lại traffic ghi khi smoke test đạt.

## 5) Failover
- Nếu primary DB lỗi: chuyển traffic sang secondary theo playbook hạ tầng.
- Bật cờ hệ thống `read-only` trong lúc chuyển vai.
- Sau failover: chạy checklist tích hợp Notification/SignalR/Background jobs.

## 6) DR Drill định kỳ
- Tần suất: mỗi tháng 1 lần.
- Bắt buộc diễn tập các tình huống:
- Restore theo thời điểm cụ thể.
- Mất primary node.
- Mất kết nối mail/notification tạm thời.
- Kết thúc drill phải có biên bản: thời gian thực tế, sai lệch so với RTO/RPO, action item.

## 7) Checklist sau sự cố
- Đối soát số lượng đơn hàng, lệnh sửa chữa, báo giá, thanh toán sau thời điểm khôi phục.
- Kiểm tra hàng đợi notification và retry queue.
- Kiểm tra cảnh báo tồn kho và các lịch nhắc bảo dưỡng tự động.
- Ghi nhận RCA và cập nhật runbook.
