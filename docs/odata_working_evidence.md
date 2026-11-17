# OData — bằng chứng làm việc

Mốc: 15/11/2026. Workspace có thay đổi chưa commit; tài liệu này không phải chứng nhận nghiệm thu hoặc hiệu năng production.

- Hai tiền tố `/odata` và `/api/v1/odata` dùng cùng EDM, giới hạn query và các controller. Route cũ được giữ. Test HTTP kiểm tra xác thực, giới hạn `$top`, phân trang và `nextLink` cho hai tiền tố; catalog AI có test riêng.
- `$skip` trực tiếp vẫn giới hạn 10.000; `nextLink` dùng `$skiptoken` để tiếp tục. Test HTTP trên hai tiền tố đi qua ngưỡng 10.000; test LocalDB kiểm tra filter, sort giảm, giá trị sort trùng và token sai trên catalog AI. Chưa có benchmark dữ liệu đại diện hoặc bằng chứng snapshot khi dữ liệu thay đổi giữa các trang.
- `ServiceOrders`, `Quotations`, `Appointments`, `Vehicles` và `Invoices` phân biệt actor staff/customer; loại actor không được nhận diện không tự nhận phạm vi customer. `Customers`, `Staffs`, `Vehicles` ẩn bản ghi xóa mềm mặc định; chỉ admin được yêu cầu `includeDeleted=true`, áp dụng cả danh sách và bản ghi theo ID. Khách hàng không thấy liên kết sở hữu đã xóa hoặc chủ cũ.
- Query audit OData chỉ trả metadata và trả null cho giá trị trước/sau. Audit mới loại thuộc tính nhạy cảm theo tên, giữ event UPDATE khi chỉ có trường nhạy cảm thay đổi. API audit thông thường chỉ hiển thị hai trường boolean `IsDeleted` và `IsActive`, ẩn toàn bộ trường chưa xác định an toàn; dữ liệu lịch sử trong database chưa bị thay đổi. Tìm kiếm audit chỉ tìm trong tên bảng và hành động.
- Hóa đơn có cả ServiceOrderId và RetailOrderId được giới hạn theo chi nhánh service order; nhánh retail không cấp quyền khi service order bị thiếu. Có test HTTP hai route và LocalDB schema tối thiểu. Customer scope giữ contract SO/retail cũ; việc đưa hóa đơn vào danh sách theo PayerId cần xác nhận nghiệp vụ.
- Test LocalDB độc lập xác minh lọc branch/customer trước phân trang cho ServiceOrders và invoice trên schema tối thiểu, cùng truy vấn HTTP catalog AI. Không suy rộng sang mọi endpoint hoặc schema production.
- Frontend không gọi trực tiếp các route OData trong phạm vi tìm kiếm `frontend/src`; REST được giữ cho các thao tác và màn hình hiện có. Việc chuyển thêm endpoint chỉ có ý nghĩa sau khi kiểm kê caller, contract, quyền và nhu cầu query.

Việc còn mở: thiết kế cách trả chi tiết audit OData đã được che mà vẫn giữ phân trang và `$select` an toàn; xử lý dữ liệu lịch sử tại nơi lưu trữ; kiểm thử SQL thực tế và phân quyền đầy đủ cho từng endpoint; đo execution plan/index với dữ liệu đại diện; nghiệm thu tương thích client và vận hành trên môi trường đích. Không đánh dấu OData hoàn chỉnh chỉ từ build hoặc test InMemory.

Lượt bổ sung: test nhóm OData đạt 47/47 có LocalDB; nhóm audit và AdminAudit đạt 8/8 có LocalDB sau khi áp dụng chính sách whitelist. Test SQL scope sử dụng schema tối thiểu, không thay thế schema production. Việc mặc định ẩn dữ liệu xóa mềm và giảm chi tiết audit REST là thay đổi contract.

Lượt tiếp theo: audit đọc thêm claim `branch_id` theo JWT; test tạo audit với entity không có BranchId đạt. Test SQL continuation ban đầu lỗi vì fixture thay toàn bộ GarageContext và khiến xác thực dùng schema AI tối thiểu; fixture được sửa để chỉ thay reader AI, sau đó test continuation và claim đạt 4/4 với LocalDB. Không suy rộng kết quả này sang hiệu năng production hoặc toàn bộ quyền.

Ngày 15/11/2026: nhóm OData và AdminAudit đạt 41/41, không skip, với LocalDB. Lượt này phải tạm loại hai file test ngoài phạm vi (`ControllerExceptionHandlingTests.cs`, `PaymentControllerTransitionTests.cs`) khỏi biên dịch vì chúng có lỗi trong lúc được thay đổi đồng thời; không coi kết quả này là build toàn backend. Phần transaction/accessor của `GarageContext.Audit.cs` cũng thay đổi đồng thời từ nguồn khác và không nằm trong phạm vi kiểm chứng của lượt này.
