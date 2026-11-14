# Module 8 — bằng chứng và việc chưa đóng

Trạng thái: **đang chuyển, chưa nghiệm thu**.

| Lát cắt | Bằng chứng | Giới hạn |
|---|---|---|
| Danh mục dịch vụ `GET /api[/v1]/service-categories/admin-list` | [CODE] Controller giữ xác thực và HTTP, chuyển phân trang/tìm kiếm qua `Application/Catalog/ServiceCategoryPageQuery` và cổng đọc; adapter EF chuyển tiếp trong Web. Hai route và dữ liệu trả về giữ hợp đồng. [TEST] HTTP kiểm tra role, tìm kiếm, phân trang và DTO cũ; LocalDB kiểm tra tìm kiếm tiếng Việt. | [OPEN] Adapter/context chưa sang Infrastructure; chưa đo hiệu năng hoặc xác nhận mọi biên collation SQL. Chưa chứng minh toàn bộ module khách hàng, xe, dịch vụ và quản trị đã chuyển. |
| Dịch vụ chuẩn `GET /api[/v1]/standard-services/admin-list` | [CODE] Tách truy vấn qua `StandardServicePageQuery` và cổng đọc; adapter EF tạm Web, giữ hợp đồng và bộ lọc. [TEST] HTTP so sánh DTO và hai route. | [OPEN] Cần kiểm thử SQL, hiệu năng và rà cả mutation/caller; chưa phải Infrastructure hoàn chỉnh. |
| Khách hàng và quyền xem xe | [CODE] GET danh sách/chi tiết, PUT và DELETE khách hàng legacy áp dụng cùng quy tắc xác định phạm vi chi nhánh như danh sách quản trị; liên kết xe đã xóa không còn cấp quyền xem xe/lịch sử. [TEST] HTTP kiểm tra nhân viên chi nhánh khác và liên kết đã xóa. | [OPEN] Quy tắc phạm vi dùng lịch hẹn/lệnh và thẻ trong ghi chú, không phải ràng buộc quyền bền vững; cần xác nhận nghiệp vụ, rà tạo khách và các caller, kiểm tra đồng thời và ownership lịch sử. |
| Tạo/sửa khách hàng legacy | [CODE] Nhân viên tạo khách được gắn dấu chi nhánh server; cập nhật bởi nhân viên giữ dấu hiện có, không nhận dấu chi nhánh tùy ý. [TEST] HTTP kiểm tra tạo → đọc → sửa → đọc và dấu chi nhánh. | [OPEN] Marker trong ghi chú không phải cơ chế ownership bền vững; cần rà các đường ghi khác, dữ liệu lịch sử và đồng thời. |
| Route quản trị xe | [CODE] Xóa các khai báo route trùng gây lỗi tạo Swagger; các đường dẫn tương ứng vẫn được tạo bởi route cấp controller và action. [TEST] Swagger phát sinh thành công trong môi trường Development. | [OPEN] Cần kiểm thử toàn diện hợp đồng và quyền các route xe; Swagger pass không chứng minh nghiệp vụ. |

Không có migration/schema mới cho lát cắt trên. Ghi riêng các rủi ro còn mở, không xem việc tách controller hoặc test InMemory là bằng chứng hoàn tất kiến trúc hay an toàn production.
