# Module 9 — bằng chứng và việc chưa đóng

Trạng thái: **đang chuyển, chưa nghiệm thu**.

Cập nhật lát cắt hồ sơ lương: truy vấn danh sách đã chuyển qua `SalaryProfileListQuery` (Application) và `SalaryProfileListReader` (Infrastructure); controller giữ phân quyền và hợp đồng REST cũ. Kiểm thử LocalDB trên schema tối thiểu đã chạy, bao gồm lọc chi nhánh, phân trang và route không truyền trang. Đây chưa phải xác nhận quyền theo chi nhánh, kết nối/DI trên production hay chuyển toàn bộ module 9; truy vấn yêu cầu lương vẫn ở Web.

| Lát cắt | Bằng chứng | Giới hạn |
|---|---|---|
| Hồ sơ lương `GET /api/salary-admin/profiles` | [CODE] Khi client gửi page/pageSize, server lọc chi nhánh, đếm và trả một trang; client không còn slice toàn bộ danh sách hồ sơ. Caller cũ không gửi trang tiếp tục nhận mảng như trước. Truy vấn danh sách đi qua Application/Infrastructure. [TEST] LocalDB schema tối thiểu kiểm tra trang/nhánh/thứ tự/quyền, frontend kiểm tra mã hóa và build. | [OPEN] Quyền admin/kế toán và phạm vi chi nhánh giữ hành vi cũ, cần thẩm định. Tổng và trang là hai truy vấn có thể lệch khi dữ liệu thay đổi; danh sách nhân viên vẫn tải toàn bộ. Chưa thử UI trong browser hoặc SQL schema triển khai. Endpoint là REST; module chưa hoàn tất Clean Architecture. |
| Yêu cầu lương `GET /api/salary-admin/requests` | [CODE] Client gửi phân trang; server lọc, đếm và trả số chờ/đã duyệt trên toàn tập sau lọc qua header. Caller cũ giữ mảng như trước. [TEST] LocalDB schema tối thiểu kiểm tra lọc, trang, số tổng và quyền; parser frontend có kiểm thử. | [OPEN] Danh sách nhân viên vẫn tải toàn bộ; quyền kế toán theo chi nhánh còn phải xác nhận. Count và trang không chung snapshot; chưa thử browser/schema production. SQL vẫn trong Web, endpoint REST. |
| Kỳ lương đã đóng | [CODE] Nhánh tạo bảng lương thủ công từ chối kỳ không OPEN như nhánh tính tự động. [TEST] HTTP InMemory kiểm tra kỳ CLOSED không sinh run. | [OPEN] Chưa kiểm tra cạnh tranh giữa đóng kỳ và tạo run trên SQL, transaction và trạng thái production. |

Không có migration/schema mới trong lát cắt này. Chưa nghiệm thu khóa kỳ lương, điều chỉnh, đối soát hoặc các module còn lại.
