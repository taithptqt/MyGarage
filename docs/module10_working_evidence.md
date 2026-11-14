# Module 10 — tiến độ và giới hạn

Trạng thái: **đang chuyển, chưa nghiệm thu**.

- [CODE] Controller khách hàng hiện vẫn gọi `ICustomerPortalService` cũ. `ICustomerPortalApplicationService` và `INotificationApplicationService` mới được đăng ký DI nhưng chưa thấy controller/worker sử dụng; kiểm thử trực tiếp service không chứng minh luồng HTTP production đã chuyển.
- [CODE/TEST] Service khách hàng mới đã sửa mã dòng xe bắt buộc, không ghi ID khách vào cột FK nhân viên và giới hạn quyền staff theo phạm vi cùng liên kết xe. LocalDB schema tối thiểu có FK kiểm thử tạo xe; test InMemory kiểm tra phạm vi và hợp đồng. Chưa xác nhận schema production.
- [CODE/TEST] Service mới kiểm tra trùng VIN/biển, cập nhật thời điểm ODO khi nhận chỉ số hợp lệ và trả dữ liệu thực tế khi dùng lại xe; test InMemory kiểm tra các nhánh này. Không tiếp tục ghi khi việc mở transaction thất bại.
- [CODE/TEST] Service mới từ chối số km âm khi tạo và cập nhật; đây là thay đổi validation so với một số nhánh cũ, cần xác nhận hợp đồng trước khi kết nối controller. Test SQL LocalDB schema tối thiểu có FK đã kiểm tra truy vấn biển/VIN trùng trên tạo và sửa, kể cả chữ hoa/thường; chưa kiểm schema production hoặc tranh chấp đồng thời.
- [OPEN] Kiểm tra trùng trước khi ghi không ngăn tranh chấp đồng thời; retry dùng lại context/entity, commit không rõ kết quả và transaction ngoài chưa được chứng minh an toàn. Cần kiểm tra SQL translation/schema thực, parity các trường hợp còn lại và thiết kế quyền ghi trước khi chuyển controller; không nối service mới chỉ dựa vào test hiện tại.
- [CODE/TEST] OData xe đã loại liên kết đã xóa và chủ cũ, giữ `IsCurrentOwner` null theo quy tắc quyền hiện có; test HTTP với bốn trường hợp. Chưa chứng minh toàn bộ quyền và truy vấn OData production.
- [TEST] Mapper trạng thái mới được so sánh với mapper cũ trên các tổ hợp trạng thái/chẩn đoán/chờ phụ tùng. Giữ nguyên thứ tự legacy không đồng nghĩa đã nghiệm thu quy tắc nghiệp vụ.
