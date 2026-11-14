# Master Blueprint: Tái cấu trúc MyGarage theo Clean Architecture và OData

**Trạng thái:** kế hoạch triển khai có điều kiện; chưa chứng minh an toàn production. Không triển khai đồng loạt. Các ví dụ và khẳng định an toàn tuyệt đối trong bản kế hoạch trước được thay thế bởi tài liệu này.

## 1. Mục tiêu, phạm vi và nguyên tắc

- Tách dần trách nhiệm Domain, Application, Infrastructure và Web API; giữ hệ thống chạy được sau mỗi lát cắt. Cấu trúc project hiện có chưa chứng minh toàn bộ mã đã tuân thủ Clean Architecture.
- Chuyển từng màn hình danh sách và bộ lọc nghiệp vụ sang truy vấn/phân trang server khi dữ liệu lớn; OData chỉ áp dụng nơi phù hợp, không bắt buộc thay mọi REST API.
- Giữ hợp đồng nghiệp vụ và API hiện hữu trong thời gian chuyển đổi; không coi việc build hoặc số lượng test pass là bằng chứng mọi màn hình tương thích.
- Bất biến bắt buộc: khuyến nghị chẩn đoán phải đi qua báo giá (Quotation), không liên kết trực tiếp sang Service Order; chiết khấu quan hệ là nguồn dữ liệu nghiệp vụ duy nhất, không dùng cột `TotalDiscount` cũ làm nguồn tính toán.
- Phân biệt **[CODE]** hành vi hiện tại, **[TEST]** hành vi được kiểm chứng và môi trường kiểm tra, **[BUSINESS]** quy tắc đã xác nhận. Đánh dấu điểm chưa đủ chứng cứ; không tự biến giả thuyết thành yêu cầu.
- Giữ UTF-8, nội dung tiếng Việt và các quy tắc trong `AGENTS.md`. Không thay đổi, xóa hay khôi phục các thay đổi có sẵn ngoài phạm vi nhiệm vụ.

## 2. Hiện trạng và các điểm cần kiểm chứng

- [CODE] `backend/backend.sln` có Web API `MyGarage`, `MyGarage.Domain`, `MyGarage.Application`, `MyGarage.Infrastructure` và project test. `backend/src` đã có ba project mới và ba enum báo giá trong `MyGarage.Domain/Enums`; các use case nghiệp vụ chưa được chuyển sang các lớp mới. OData và các hosted service đã có trong ứng dụng hiện tại. Dockerfile đã bổ sung sao chép project reference cho bước restore, nhưng chưa xác nhận build image trong môi trường không có Docker.
- [CODE] `Quotation.ServiceOrderId` liên kết báo giá với lệnh sửa chữa; luồng áp dụng báo giá hiện xử lý lệnh đã tồn tại. `VersionNo` là phiên bản nghiệp vụ, không phải SQL Server `rowversion`. Một số nhánh gắn nhãn phát sinh khi `VersionNo > 1`, nhưng không kết luận mọi V2 là cùng một loại nghiệp vụ; khảo sát thêm clone, thay thế, bổ sung, bảo hiểm và chẩn đoán.
- [CODE] Tạo báo giá dùng `Max(VersionNo) + 1`; `GarageContext` cấu hình unique `(ServiceOrderId, VersionNo)`. Có khả năng tranh chấp đồng thời, cần kiểm thử SQL Server có đồng bộ hai luồng và xác nhận API trả lỗi, rollback và retry.
- [CODE] GET chiết khấu báo giá có `[Authorize]`; đoạn controller/service đã khảo sát chưa thấy kiểm tra quyền sở hữu/chi nhánh. Đây là nguy cơ cần tái hiện HTTP, không khẳng định toàn bộ môi trường bị ảnh hưởng khi chưa kiểm tra các lớp bảo vệ khác. PUT cần kiểm thử riêng; 403 do sai vai trò không chứng minh có kiểm tra chi nhánh.
- [CODE] OData hiện loại `$expand` khỏi `AllowedQueryOptions`. `MaxExpansionDepth = 0` **tắt kiểm tra giới hạn độ sâu**, không phải cấm `$expand`.
- [CODE] OData hiện cấu hình `PageSize = 100` và `MaxTop = 100`; không tự đổi mặc định khi chưa đánh giá hợp đồng client. `collectPages.js` dùng các header `X-Page`, `X-Page-Size`, `X-Total-Pages`, `X-Total-Count` ở các luồng legacy; việc gom toàn bộ trang không phải giải pháp cho màn hình dữ liệu lớn.
- [TEST] Các test LocalDB hiện tại bao gồm trường hợp tạo schema thử có loại bỏ index legacy và khóa ngoại để chạy được; chúng không chứng minh tạo schema nguyên trạng hoặc migration production hoạt động. Cần kiểm thử riêng các đường đó.
- [CODE/OPEN] Một nhánh duyệt báo giá bảo hiểm bắt và bỏ qua lỗi tạo yêu cầu vật tư (`QuotationWorkflowService.cs`); nhánh dự phòng webhook bán lẻ lưu trạng thái đơn trước bước trừ tồn/gửi email (`WebhookController.cs`). Đây là đường lỗi cần kiểm chứng, chưa chứng minh dữ liệu production đã sai.
- [CODE/OPEN] Token báo giá công khai nằm trong route (`QuotationController.cs`); `GlobalExceptionHandler.cs` ghi `Request.Path` khi gặp ngoại lệ chưa xử lý. Có nguy cơ token xuất hiện trong log ở đường lỗi, chưa xác minh qua HTTP hoặc log production.
- [CODE/OPEN] Rate limiter tạo khóa có user ID, nhưng pipeline đặt `UseRateLimiter` trước `UseAuthentication` (`Program.cs`); kiểm thử pipeline thật để xác định tác động, không khẳng định đã tái hiện.
- [CODE] CI hiện có backend build/test, frontend verify và job build image; phải phân biệt cấu hình job với bằng chứng job đã chạy thành công. Một số test SQL chỉ chạy khi được bật, nên báo cáo số test pass/skip và môi trường.
- [OPEN] Xác nhận quyền sử dụng và cấu hình giấy phép AutoMapper 15.1.4 theo thực tế trước rollout; không giả định có giấy phép chỉ vì ứng dụng build hoặc chạy.
- [OPEN] Trạng thái áp dụng migration trên môi trường triển khai, dữ liệu lịch sử, quyền thực tế, cam kết gửi sự kiện và hiệu năng Azure SQL phải được xác nhận trước khi đưa thay đổi tương ứng vào vận hành.

## 3. Kiến trúc đích và cách chuyển từng module

### 3.1 Quy tắc phụ thuộc

| Lớp | Trách nhiệm | Phụ thuộc được phép |
|---|---|---|
| Domain | Bất biến và mô hình nghiệp vụ đã xác nhận; không phụ thuộc HTTP, EF hay UI | Không phụ thuộc Application, Infrastructure hoặc Web |
| Application | Use case, chính sách, hợp đồng repository/transaction và DTO nghiệp vụ | Domain; không phụ thuộc triển khai Infrastructure hoặc controller |
| Infrastructure | EF Core, cơ sở dữ liệu, lưu trữ, tích hợp và worker | Application/Domain; triển khai interface hướng vào trong |
| Web API | Xác thực, phân quyền tài nguyên, HTTP, chuyển đổi request/response, composition root | Application và Infrastructure qua đăng ký DI; không đưa truy vấn/luồng nghiệp vụ mới vào controller |
| Frontend | Tương tác, trạng thái UI và gọi API; không quyết định quyền server | Hợp đồng API, không phụ thuộc nội bộ backend |

Các project và namespace hiện hữu có thể cần giai đoạn tương thích. Giữ `MyGarage.Models` tạm thời khi cần giảm thay đổi snapshot, nhưng tên namespace không chứng minh mô hình EF hoặc database không đổi. Kiểm tra dependency graph bằng project references và kiểm thử; tránh dự án mới chỉ là lớp vỏ còn toàn bộ nghiệp vụ phụ thuộc ngược về Web.

### 3.2 Danh mục chuyển đổi

Lập danh sách thực tế endpoint, màn hình, worker, schema, quyền và chủ sở hữu cho từng nhóm trước khi ước lượng; theo dõi trạng thái `chưa khảo sát / đang chuyển / đã nghiệm thu / còn giới hạn`. Không coi thứ tự dưới đây là xác nhận mọi nhóm đã hoàn tất:

1. Xác thực, phiên đăng nhập, quyền và chi nhánh; nền tảng lỗi/ghi log.
2. Chẩn đoán → báo giá → lệnh sửa chữa, bảo hiểm, chiết khấu; pilot báo giá ở mục 5.
3. Đặt lịch/tiếp nhận, điều phối, xưởng và QC; giữ nhóm lịch, phân công và vòng đời lệnh.
4. Kho, vật tư, nhà cung cấp, đơn mua, nhập/xuất/kiểm kê; bảo toàn trạng thái và số lượng.
5. Khách hàng, xe, danh mục dịch vụ và phụ tùng; tài khoản và danh mục quản trị.
6. Bán lẻ, voucher, thanh toán/PayOS, hóa đơn, công nợ, kế toán, nhân sự/lương và báo cáo.
7. Cổng khách hàng, chat/chatbot và SignalR, AI dự báo/tri thức/điều phối, báo cáo, nội dung home/news/contact, thông báo, tác vụ nền, file và tích hợp ngoài; kiểm kê thêm các trang Horizon và module còn lại.

Mỗi nhóm phải có: ma trận nghiệp vụ và quyền; API/UI liên quan; bất biến và dữ liệu cũ; transaction và tác dụng ngoài; migration; chuyển đổi tương thích; bài test và kết quả; chỉ số hiệu năng; phương án dừng/khôi phục. Pilot không tự chứng minh các nhóm khác an toàn. Đây là thứ tự ưu tiên dự kiến, không phải khẳng định các nhóm độc lập. Có thể khảo sát và chuẩn bị nhóm kế tiếp khi nhóm trước đang kiểm thử, nhưng không chuyển giao quyền ghi hoặc phát hành use case phụ thuộc trước khi các hợp đồng và điều kiện liên quan đạt nghiệm thu. Điểm mở chưa được chấp nhận phải được ghi rõ; không đổi thứ tự chỉ để vượt cổng.


### 3.3 Sơ đồ thư mục đích và ánh xạ chuyển đổi

Cấu trúc sau là hướng tổ chức đích, không yêu cầu di chuyển hàng loạt hay tạo đủ thư mục rỗng ngay. Nghiệp vụ vẫn có thể nằm ở cấu trúc cũ trong giai đoạn chuyển tiếp; mỗi use case có một đường xử lý và một nguồn ghi rõ ràng.

```text
backend/
  MyGarage.csproj, Controllers/, Program.cs  # Web hiện hành và composition root
  src/MyGarage.Domain/                      # mô hình, giá trị và chính sách thuần
  src/MyGarage.Application/                 # use case, contract/port và DTO theo module
  src/MyGarage.Infrastructure/              # EF, repository, integrations và worker
  tests/MyGarage.Tests/                     # unit, API contract, SQL integration
Frontend/src/features/<module>/             # màn hình, API, state theo tính năng
```

- Chuyển theo use case, không theo thao tác kéo toàn bộ `Models/`, `Controllers/` hay `Services/`. Ánh xạ mỗi phần: controller giữ HTTP/authorization → application điều phối use case và port → domain giữ quy tắc đã xác nhận → infrastructure cung cấp EF/tích hợp; Web đăng ký DI. Các DTO HTTP không tự động là domain entity; project tham chiếu theo bảng 3.1, kiểm tra vòng phụ thuộc và xuất bản runtime.
- Trước khi chuyển entity, kiểm tra mọi namespace, interface, annotation và EF configuration; ví dụ `Appointment` đang phụ thuộc `IAuditEntity` ở cấu trúc cũ. Tách contract thực sự độc lập theo từng lát cắt, không cho Domain tham chiếu ngược Web. Kiểm tra thay đổi assembly, reflection, serialization, mapping, DI, migration và model snapshot; giữ namespace chỉ là biện pháp tương thích tạm.
- Mỗi module duy trì bảng ánh xạ đường dẫn cũ → mới, endpoint/route và caller, job/hub liên quan, chủ sở hữu dữ liệu và nguồn ghi. Chỉ xóa đường cũ khi đã kiểm kê caller, kiểm thử chuyển đổi và không còn phụ thuộc; không đổi cấu trúc frontend hàng loạt chỉ để giống backend.

### 3.4 Bảng điều phối module và kịch bản bắt buộc

Bảng sau là **hàng đợi thực hiện**, không phải báo cáo đã chuyển xong. Trạng thái tất cả các hàng mặc định là *cần kiểm kê/đối chiếu*; một số trang đã có API phân trang server, nhưng phải xác minh từng tab, bộ lọc, caller và trạng thái code trước khi đánh dấu. Đây là backlog linh hoạt: chỉ cần ổn định hợp đồng của phụ thuộc liên quan trước khi chuyển đường ghi, không yêu cầu hoàn tất trọn một hàng mới khảo sát hay làm lát cắt khác. Baseline schema/backup là kiểm tra hiện trạng, không tự chạy migration trên production.

| Thứ tự | Lát cắt và điểm bắt đầu kiểm kê | Phụ thuộc và nghiệm thu đặc thù |
|---|---|---|
| 0 | Nền hệ thống: `AuthController`, branch, session/token, DI, errors, audit, OData và hosted service | Baseline API/role/branch, token, DI/startup, SignalR/worker; xác minh quyền theo tài nguyên và schema/backup. Trước pilot test nguy cơ GET/PUT discounts và tranh chấp phiên bản; 403 do role không chứng minh scope. |
| 1 | Pilot chẩn đoán → báo giá → lệnh: `QuotationController`, `QuotationWorkflowService`, `ServiceOrderQuotePage`, public quotation | Đối chiếu diagnosis, lệnh, discount quan hệ, kho, bảo hiểm, thanh toán; V1/clone/supersede/phát sinh, public OTP, cạnh tranh và retry theo mục 5. Không tự gộp mọi V2. |
| 2 | Đặt lịch và tiếp nhận: `AppointmentsController`, `BookingPage`, `MyAppointmentsPage`, `ReceptionAppointmentsPage` | Khách/chi nhánh, khung giờ, nhóm lịch, trạng thái, timezone, polling và notification; nhóm không bị chia sai trang; kiểm thử ngày biên và SQL. |
| 3 | Xưởng, điều phối, kỹ thuật, QC và bàn giao: `workshop/`, `qc/`, `DispatchCenterPage`, `QCDashboard` | Phân công/điều chuyển, diagnosis, QC, kho và lệnh; realtime/SignalR, xung đột cập nhật và quyền theo vai trò. Kiểm kê `ReceptionOrdersSection`, `WorkshopRoleHistoryPage` theo dữ liệu ghép và nhóm. |
| 4 | Kho và vị trí: `InventoryAdminPage`, `PartsPage`, `StockHistoryPage`, `BayManagementPage`, yêu cầu/trả vật tư | Trạng thái tồn, đặt trước/xuất/trả, số lượng đồng thời, audit, nhiều kho/chi nhánh; bốn bảng/tab bay, báo cáo và export được kiểm tra riêng. |
| 5 | Mua hàng: `PurchaseOrderPage`, `GoodsReceiptPage`, nhà cung cấp | Duyệt, nhập một phần/nhiều lần, hủy và đối soát số lượng/giá, tương thích kho; kiểm tra giao dịch và thao tác lặp. |
| 6 | Bán lẻ và voucher: `PartsSalesPage`, `RetailCartPage`, `MyRetailOrdersPage`, `VoucherManagementPage` | Giá/thuế, tồn và đơn, khuyến mãi/discount quan hệ, voucher phân bổ/sử dụng đồng thời; từng tab campaign, approval, redemption và báo cáo/export. |
| 7 | Thanh toán, hóa đơn và đối soát: `payment/`, controller PayOS/payment/invoice/accounting, trang Horizon | Webhook lặp/đến sai thứ tự/sai chữ ký, thanh toán từng phần, timeout, hủy/hoàn tiền nếu có, đối soát, công nợ và replay; không mặc định giao dịch bên ngoài rollback cùng DB. |
| 8 | Khách hàng, xe, danh mục và quản trị: `AdminPage`, `VehicleManagementPage`, `ServicesPage`, `ServiceCategoriesPage` | Sở hữu xe, chi nhánh, lookup/modal, dữ liệu tham chiếu, phân quyền CRUD; giá dịch vụ và danh mục theo hành vi đã xác nhận, không tự giả định giá theo chi nhánh. |
| 9 | Nhân sự, lương và kế toán: `AdminSalaryPage`, `PayrollRunPage`, `MyPayslipPage`, trang `horizon/views/admin/accounting` | Kỳ lương/khóa sổ, điều chỉnh/hoa hồng/thuế, dữ liệu nhạy cảm, audit, báo cáo và số tổng; không tính lại hoặc ghi đè kỳ đã khóa khi chưa có quy tắc. |
| 10 | Cổng khách hàng, chat và thông báo: `customerPortal/`, `chat/`, hubs, notification và worker | Ownership/link token, quyền hub/group, gửi lại/kết nối lại, delivery trùng, file và dữ liệu cá nhân; chức năng realtime và tác vụ nền tiếp tục hoạt động sau chuyển DI. |
| 11 | AI, dashboard, báo cáo và nội dung: `AiForecastController`, `AiKnowledgeBaseController`, `AiSmartDispatchingController`, `chatbot/`, `home/`, `news/`, `contact/` | Kiểm kê cấu hình/model/knowledge, quyền, chi phí và timeout, dự phòng; báo cáo đối chiếu nguồn số liệu, hiệu năng, export. Không mặc định không dùng vì thiếu màn hình riêng. |
| 12 | Phần dùng chung còn lại: search, file/storage, gate pass, approval, audit, jobs và API chưa được ánh xạ | Đối chiếu route/worker/hub/DI theo inventory để đóng các mục chưa gán; kiểm tra vòng đời, retry, quyền, bí mật và callers ngoài ứng dụng. |

Các tương tác xuyên hàng (ví dụ báo giá–kho–thanh toán) được nghiệm thu liên module sau mỗi thay đổi liên quan; không dùng trạng thái hoàn tất của một hàng để suy ra các đường tích hợp đã an toàn.

### 3.5 Sổ theo dõi từng use case và màn hình

Với mỗi route, trang hoặc tab, ghi một dòng gồm: ID; module; đường dẫn code/route/verb; actor và phạm vi (khách, staff, admin, public token, worker, webhook/máy khi có); chức năng/filter/sort/page/count/export/polling; nguồn dữ liệu và transaction; caller/worker/hub; hợp đồng cũ/mới; trạng thái `chưa khảo sát / đang làm / cần xác nhận / đã kiểm thử / đã nghiệm thu`; bằng chứng tách `[CODE]` / `[TEST]` / `[BUSINESS]`, test + môi trường + kết quả; migration/rollback; chủ xử lý và điểm còn mở. Liệt kê cả trang không phải danh sách và tích hợp ngoài. Kiểm kê từ route frontend, controller/backend, workers và scheduled jobs, không dựa riêng vào tìm `.slice()`.

Các điểm `.slice()` hiện thấy trong `AdminSalaryPage`, `VoucherManagementPage`, `BayManagementPage`, `QCDashboard`, `ReceptionOrdersSection` là tín hiệu cần rà; không coi danh sách này là đầy đủ hoặc khẳng định mọi trang khác đã chuyển. Kiểm tra từng tab, tổng số, trang cuối, dữ liệu tham chiếu và xuất toàn bộ. Thay kết luận theo kết quả kiểm tra thực tế chứ không theo tên API.


**Các mục khởi đầu đã nhận diện — chưa phải kết quả kiểm thử:**

| ID | Phạm vi/caller | Bằng chứng [CODE] | Kiểm thử/gate cần hoàn tất trước chuyển hoặc phát hành liên quan |
|---|---|---|---|
| Q-SEC | GET/PUT `/api/quotations/{id}/discounts`; khách, staff cùng/khác chi nhánh | GET có authorize nhưng đoạn đã đọc chưa thấy owner/branch; PUT có guard vai trò | HTTP: khách A/B, staff đủ vai trò cùng/khác chi nhánh, anonymous và actor hợp lệ; assert cả payload và mutation DB. Phân biệt cấm do role và cấm do scope. [TEST: chưa xác minh trong lượt lập kế hoạch] |
| Q-VER | Tạo báo giá cho một service order | `Max(VersionNo)+1`, DB unique theo service order/version | Hai SQL connection với đồng bộ sau bước đọc; kiểm tra số phiên bản, rollback, phản hồi, lỗi trùng và retry. Không kết luận production xảy ra lỗi trước khi tái hiện. [TEST: chưa xác minh] |
| Q-MAT | Nhánh duyệt bảo hiểm và tạo yêu cầu vật tư | Có khối catch bỏ qua lỗi tạo yêu cầu | Chốt yêu cầu nghiệp vụ; tiêm lỗi trước/giữa/sau tạo vật tư, kiểm tra trạng thái báo giá/lệnh/yêu cầu và số lượng đặt trước, test duyệt lặp/tạo lại/đồng thời, có và không có transaction ngoài, thiếu kho; phân biệt báo giá không có phụ tùng với lỗi. Thiết kế cảnh báo/đối soát và khôi phục. [TEST/BUSINESS: cần xác minh] |
| P-STOCK | Webhook bán lẻ dự phòng → trạng thái đơn → tồn → email | Nhánh dự phòng lưu trạng thái trước các bước sau | Tiêm lỗi giữa từng bước, webhook trùng/sai thứ tự; đối chiếu đơn, thanh toán, tồn và thông báo; chốt điểm dừng/replay. [TEST/BUSINESS: cần xác minh] |
| Q-TOKEN-LOG | Link báo giá công khai và lỗi chưa xử lý | Token nằm trong path; global exception handler ghi path trên đường lỗi | Dùng token thử giả trong môi trường test, kích hoạt lỗi chưa xử lý và xác minh log ứng dụng không chứa token thô; kiểm tra access log/proxy/collector theo cấu hình triển khai, exception và URL; xử lý log cũ theo chính sách sau khi xác nhận phạm vi. [TEST: chưa xác minh] |
| PLATFORM-RATE | Pipeline xác thực và rate-limit | Partition dùng user ID; middleware rate-limit đứng trước authentication | Test HTTP thực tế: 2 user cùng IP và 1 user nhiều request, anonymous, endpoint có/không policy; so partition và 429, xem proxy/IP tin cậy và tác động auth/OTP/OData trước khi đổi thứ tự middleware. [TEST: chưa xác minh] |
| DB-SCHEMA | Fresh schema / nâng cấp DB | LocalDB test translation có bỏ index và FK khỏi script thử | Chạy tạo schema và migration nguyên trạng trên SQL Server riêng, rồi upgrade dữ liệu đại diện và diễn tập restore; ghi lỗi/blocker. [TEST: chưa xác minh nguyên trạng] |

Các ID là điểm bắt đầu, không bao phủ mọi luồng. Ghi kết quả thật, revision, môi trường, test bị skip và người xử lý khi triển khai; không sửa trạng thái thành đã đạt chỉ dựa trên văn bản này.

### 3.6 Quy trình thực hiện cho mỗi lát cắt

1. **Đầu vào:** xác định một use case/màn hình, actor và phạm vi; chụp baseline hợp đồng, số liệu và test liên quan; xác định chủ sở hữu, phụ thuộc và tài liệu quyết định. Nếu chưa xác nhận nghiệp vụ hoặc dữ liệu đủ cho thay đổi, tiếp tục khảo sát độc lập; không đoán để chuyển đường ghi.
2. **Thiết kế:** chọn ranh giới lớp và ánh xạ code; xác định duy nhất nguồn ghi, transaction, idempotency, event, schema và tương thích cũ/mới. Chọn OData hoặc REST theo khả năng biểu diễn và kiểm soát query; không ép OData cho mutation/phép tổng hợp không phù hợp.
3. **Thực hiện:** thay từng use case/backend rồi frontend tương ứng; giữ route cũ khi cần, kiểm soát đồng thời hai phiên bản, worker/job chỉ xử lý theo thiết kế và event có thể phát lại và được kiểm soát theo cam kết/idempotency thực tế, không giả định gửi đúng một lần. Di chuyển cấu trúc và thay đổi hành vi/schema thành những bước review được.
4. **Nghiệm thu:** test quyền đúng/sai, nghiệp vụ và lỗi, DB thật phù hợp, hợp đồng frontend, migration và đường tích hợp; đo baseline và ngưỡng đã thống nhất trên dữ liệu đại diện. Ghi test bị bỏ qua, kết quả và rủi ro; không tuyên bố hoàn tất vì build xanh.
5. **Chuyển giao:** có người chịu trách nhiệm, runbook, giám sát, điều kiện dừng và khôi phục theo loại thay đổi; xác nhận caller cũ và nguồn ghi, rồi mới đánh dấu hoàn tất hoặc phát hành. Điểm chưa đạt được ghi rõ và không coi như đã nghiệm thu.

## 4. Khảo sát nghiệp vụ và quyền trước khi thay đổi

Lập ma trận cho các luồng tạo, clone, thay thế, duyệt nội bộ, quyết định khách hàng đã đăng nhập, link công khai, báo giá bổ sung, hủy/điều chỉnh và bảo hiểm. Với từng luồng ghi:

1. Tên luồng và đầu vào; 2. actor/vai trò; 3. phạm vi chi nhánh, chủ sở hữu và quyền đọc/ghi; 4. điều kiện trước và trạng thái; 5. bảng/cột thay đổi; 6. transaction và tác vụ ngoài; 7. chiết khấu quan hệ; 8. tác động kho, bảo hiểm, thanh toán/công nợ; 9. lặp, đồng thời, lỗi và kết quả; 10. dẫn chứng code/test, môi trường; 11. quy tắc nghiệp vụ chưa xác nhận.

Khảo sát dữ liệu bằng truy vấn chỉ đọc, hạn chế dữ liệu nhạy cảm; chỉ dùng bản sao DB khi có cơ chế bảo vệ phù hợp. Chốt riêng các hành vi thêm, thay thế, hủy, điều chỉnh, loại đã thực hiện và quyết toán; không giả định mọi báo giá V2 cộng dồn hoặc tự đưa bộ mã hành động mới thành quy tắc chính thức.

Ma trận quyền phân biệt khách hàng sở hữu/không sở hữu, nhân viên cùng/khác chi nhánh và quản trị viên có phạm vi được cấp. Test GET/PUT discounts cho cả quyền hợp lệ và không hợp lệ, khách A/B, nhân viên hai chi nhánh, anonymous và vai trò không phù hợp; xác định 403 do role hay branch. Mở rộng khảo sát các endpoint báo giá khác, public token và background jobs. Ưu tiên xử lý nguy cơ phân quyền đã xác nhận trước khi chuyển module.

## 5. Pilot báo giá: transaction, đồng thời, sự kiện

### 5.1 Giao dịch và retry

- Xác định phạm vi ghi báo giá, dòng chi tiết, chiết khấu quan hệ, đồng bộ lệnh và các tác vụ vật tư trong từng luồng hiện có; không giả định tất cả đều dùng cùng một transaction. Không thay đổi quyền hoặc thứ tự nghiệp vụ chưa được kiểm chứng.
- Điều phối transaction thực tế ở application/orchestrator, dùng `CreateExecutionStrategy` phù hợp khi bật retry SQL Server; không thực hiện tác vụ ngoại vi trong khối có thể retry. Giữ tương thích chính sách `scripts/check-transaction-policy.ps1`, nhưng không coi regex pass là chứng minh đúng transaction.
- Xử lý commit không rõ kết quả bằng mã thao tác bền vững hoặc xác minh commit phù hợp; không mặc định rollback và thực thi lại. Khi retry cần trạng thái DbContext sạch, tránh dùng lại entity dirty; rollback lỗi không được che mất lỗi gốc. Kiểm thử nested transaction, commit mất ACK, rollback lỗi, hủy request và retry.
- Không thêm unique index `(ServiceOrderId) WHERE IsAppliedToServiceOrder = 1`: có thể chặn nhiều báo giá đã áp dụng hợp lệ. Ràng buộc chống áp dụng trùng phải dựa trên bất biến đã xác nhận, có kiểm tra dữ liệu cũ và test báo giá gốc + các phiên bản tiếp theo.

### 5.2 Idempotency và kiểm soát đồng thời

- Mỗi thao tác có key ổn định do client hoặc server cấp; lưu riêng hash nội dung yêu cầu đã chuẩn hóa và ngữ cảnh operation, tài nguyên, chủ thể/quyền. Không đưa raw public token, URL có bí mật hay PII không cần thiết vào log/lưu trữ. Xác thực và kiểm tra quyền hiện hành trước replay; token công khai là quyền truy cập, không chứng minh danh tính cá nhân.
- Trước khi chuyển mỗi mutation, chốt cách client giữ và gửi lại cùng key cho cùng thao tác qua timeout/retry, thời điểm tạo key mới, TTL, định dạng replay và cách kiểm thử public link. Nếu client cũ không gửi key, server tạo một key mới cho mỗi request không chống trùng: cần cơ chế tương thích có định danh ổn định đã kiểm chứng hoặc giữ hành vi cũ có giới hạn và kế hoạch chuyển client; không tự suy ra cùng thao tác chỉ vì payload giống nhau. Test cùng key/cùng payload, cùng key/khác payload, retry sau mất ACK, hai thao tác hợp lệ giống nội dung, request đồng thời và client cũ.
- Cùng key/ngữ cảnh/nội dung đã hoàn thành: trả kết quả theo hợp đồng; cùng key khác nội dung hoặc ngữ cảnh: từ chối. Định nghĩa phản hồi khi đang xử lý, lỗi, hết hạn, quyền bị thu hồi và TTL theo nghiệp vụ; không mặc định lỗi unique luôn là 409 hoặc request trùng luôn được trả ngay.
- Lựa chọn triển khai pilot sau thử nghiệm: **(A)** ghi marker/kết quả cùng transaction nghiệp vụ và outbox, xử lý chờ khóa/unique/timeout; hoặc **(B)** giữ chỗ `PENDING` bằng transaction độc lập, có owner, lease, mã từng lượt nhận, phục hồi và kiểm soát tiến trình cũ. Với B, `COMPLETED` phải commit cùng nghiệp vụ/outbox; PENDING đã commit không tự rollback. Khi kết quả commit chưa rõ, xác minh trước khi giải phóng hoặc cho chạy lại.
- `Status` đơn lẻ hoặc `VersionNo` không bảo vệ mọi thay đổi đồng thời. Xác định version cho phạm vi báo giá, chi tiết, chiết khấu và lệnh; mọi đường ghi liên quan phải tham gia cùng transaction. Nếu dùng SQL Server `rowversion`, DB tự sinh khi dòng được UPDATE; nếu dùng revision do ứng dụng quản lý, định nghĩa cập nhật có điều kiện. Không đánh dấu toàn bộ entity `Modified` bừa bãi. Client gửi version đã xem, có xử lý xung đột; triển khai tương thích các client/route cũ và link công khai theo hợp đồng đã chốt.

### 5.3 Transactional outbox

Khi nghiệp vụ yêu cầu thông báo/tích hợp đáng tin cậy, lưu bản ghi outbox và thay đổi nghiệp vụ trong **cùng transaction SQL**. Channel trong RAM chỉ có thể làm tín hiệu thức worker, không là nguồn bền bỉ. Xác định loại sự kiện, người nhận, thứ tự, dữ liệu và yêu cầu gửi cho từng luồng trước khi áp dụng.

- Worker nhận việc nguyên tử: chỉ lấy việc đến lịch hoặc lease hết hạn, ghi owner và token mới cho mỗi lượt nhận; transaction claim ngắn. Thử SQL dưới isolation/RCSI thực tế, tránh giả định `READPAST` bỏ qua mọi loại khóa hay bảo đảm FIFO. Hoàn tất/gia hạn phải kiểm tra trạng thái và token của lượt nhận; quy định chính sách khi lease hết hạn và có phục hồi worker chết.
- Dùng thời gian UTC từ nguồn thống nhất cho lease/retry, theo dõi đồng hồ và failover; UTC tự nó không loại trừ clock skew. Không áp đặt thay đổi thời gian nghiệp vụ hiện hữu; làm rõ timezone và hợp đồng từng trường.
- Phân biệt lỗi tạm thời/vĩnh viễn, backoff và giới hạn retry cấu hình được; có trạng thái lỗi cần can thiệp, cảnh báo, replay, retention và bảo vệ PII. Không đặt cứng 5 lần hoặc 30 ngày khi chưa có yêu cầu.
- Có thể phát lại sau khi gửi nhưng trước khi ghi nhận. Sử dụng event ID ổn định và chống trùng ở nơi nhận khi hỗ trợ; không hứa exactly-once email/SMS hoặc bảo đảm giao thành công vô điều kiện. Đo và giám sát backlog, tuổi sự kiện, lỗi và lease.


### 5.4 Bản thiết kế đầu vào cho pilot

Chọn **một** hành động cụ thể sau baseline (ví dụ một luồng quyết định báo giá đã xác định actor/điều kiện); không chuyển đồng thời toàn bộ `QuotationWorkflowService`. Trước khi code, ghi quyết định theo mẫu: route và caller; actor/scope; đầu vào và response tương thích; bảng đọc/ghi và bất biến; transaction hiện tại và transaction đích; cổng giao tiếp với ServiceOrder/discount/vật tư; điều kiện retry/idempotency; migration/rollback; test và ngưỡng.

Trước khi chọn port cho pilot, `IGarageContext` hiện công khai `DbSet` và kiểu EF; không chuyển nguyên interface này vào Application rồi coi ranh giới đã sạch. Ghi hợp đồng tối thiểu cho use case (truy vấn/ghi, phân quyền và transaction), implementation EF ở Infrastructure, adapter tương thích gọi code cũ nếu cần, vòng đời DbContext và cách giữ cùng transaction; kiểm thử dependency graph/DI, SQL translation và rollback trước khi chuyển writer. Không tách repository chung bằng mọi giá nếu query đặc thù không phù hợp; lựa chọn phải review được.

Ánh xạ dự kiến để thử: controller giữ xác thực/HTTP và gọi application use case; application dùng port cho quyền theo tài nguyên, truy vấn/ghi và transaction; domain chứa quy tắc thuần đã xác nhận; infrastructure triển khai EF và tích hợp. Đây là sơ đồ ứng viên, không khẳng định có sẵn những class hay hợp đồng đó. Giữ adapter tới luồng cũ trong phạm vi use case chưa chuyển; chỉ chuyển quyền ghi sau khi chứng minh API cũ/mới, DI và transaction cùng đáp ứng hợp đồng.

Điều kiện rời pilot: Q-SEC/Q-VER, Q-MAT và Q-TOKEN-LOG có kết luận theo phạm vi liên quan; hợp đồng key của client cũ/mới và port/adapter có kiểm thử; có test quyền hợp lệ/sai phạm vi, nghiệp vụ gốc/biến thể, cạnh tranh và lỗi transaction; với event thật sự cần độ tin cậy, có kiểm thử outbox theo mục 5.3. Ghi rõ các luồng khác trong service chưa chuyển; pilot pass không cho phép coi toàn bộ báo giá đã hoàn tất. Nếu quy tắc chủ sở hữu nghiệp vụ chưa xác định hoặc schema thật không thể nâng cấp/khôi phục, tiếp tục việc độc lập nhưng không phát hành phần phụ thuộc.

## 6. Phân quyền và OData

### 6.1 Phân quyền tài nguyên

Xây dựng kiểm tra server theo ma trận đã xác nhận cho cả đọc và ghi: xác thực, vai trò, chi nhánh hoặc quan hệ sở hữu đối với từng tài nguyên. Chi nhánh có thể nằm trực tiếp hoặc qua quan hệ; khách hàng, xe và dữ liệu dùng chung không mặc định có `BranchId`. Không tin `branchId` từ client nếu không kiểm tra quyền; không mặc định mọi quản trị viên có mọi phạm vi.

Global query filter chỉ là một lớp bổ sung sau khi kiểm tra ảnh hưởng lên navigation, dữ liệu khách hàng, truy vấn `IgnoreQueryFilters`, tác vụ nền và các cách ghi. Bypass chỉ cho use case được phép, phạm vi hẹp, có audit phù hợp; scope lồng nhau phải khôi phục giá trị cũ, kiểm tra AsyncLocal lan sang task, DbContext lifecycle và pooling. Kiểm thử chéo hai chi nhánh/khách hàng, quản trị viên và worker trước khi mở rộng.

### 6.2 OData và danh sách frontend

- Ưu tiên query có scope quyền **trước** filter/paging, projection trường cần dùng và `AsNoTracking` cho các truy vấn chỉ đọc phù hợp; không đưa entity hoặc trường nhạy cảm ra EDM theo mặc định.
- Endpoint danh sách không cho `$expand` thông qua allowlist query option. `MaxExpansionDepth = 0` là tắt kiểm tra giới hạn độ sâu; nếu một endpoint cho phép expand, đặt giới hạn hữu hạn riêng và kiểm thử trực tiếp. Giới hạn query, node, hàm, sort, top/skip và cột được phép theo nhu cầu; giới hạn không tự chứng minh không có table scan.
- Mỗi màn hình: tìm kiếm, trạng thái, ngày, quyền và bộ lọc nghiệp vụ phải chạy trên server **trước** đếm và phân trang; tổng/stats phải định nghĩa rõ toàn phạm vi hay sau lọc. Thứ tự ổn định có khóa duy nhất, reset trang khi đổi bộ lọc, tránh response cũ ghi đè, xử lý lỗi và làm mới sau CRUD. Với nhóm dữ liệu, phân trang đơn vị nhóm và giữ nguyên quy tắc thành viên; kiểm thử SQL translation.
- Danh sách dài đi tuần tự dùng cursor/keyset khi phù hợp, với sort duy nhất và cursor gắn bộ lọc/phạm vi; trang số có thể dùng offset với giới hạn rõ ràng. Không coi `MaxSkip = 10.000` là giải pháp truy cập mọi bản ghi; thiết kế đường truy cập hoặc xuất dữ liệu phù hợp. Xác định hành vi khi dữ liệu thay đổi giữa các trang.
- Đối chiếu từng màn hình với API: dữ liệu modal và dropdown, thống kê, export, polling, toàn bộ bộ lọc, role/branch, kết quả rỗng và dữ liệu lớn. Với dữ liệu lớn, không tải toàn bộ dataset ẩn ở frontend để phục vụ bảng đã phân trang; dữ liệu tham chiếu lớn cần cơ chế tìm kiếm/phân trang riêng. Chỉ số, index và execution plan phải được đo trên dữ liệu đại diện.

## 7. Tương thích API, migration và triển khai

### 7.1 Hợp đồng API

Kiểm kê route/verb, payload, phân quyền, mã lỗi, `message`, headers phân trang, DTO và các frontend/worker sử dụng. Giữ route hiện hành, gồm `/api/quotations`, và response cũ khi chưa chuyển xong người dùng; version/dual route và Swagger dùng khi cần, phải tránh route/operationId trùng. Chỉ thay đổi hợp đồng theo chiến lược rollout đã kiểm thử; không mặc định mọi endpoint có cùng kiểu envelope hoặc bốn header. Giữ tính tương thích với `collectPages.js` và client cũ nơi đang dùng. Test cả lỗi, phân trang, link công khai và chức năng xuất file.

### 7.2 Schema và khôi phục

1. Refactor cấu trúc riêng với kỳ vọng không đổi schema; đối chiếu model/snapshot, script SQL và kiểm thử `has-pending-model-changes`. Giữ namespace có thể giảm thay đổi, không bảo đảm không có migration hoặc mất dữ liệu.
2. Thay đổi schema có migration chính thức: outbox, idempotency, version hoặc index chỉ sau khi chốt thiết kế. Kiểm tra dữ liệu trùng/vi phạm, null, kích thước và thứ tự migration; chạy trên DB sạch và bản sao dữ liệu đại diện được bảo vệ.
3. Chuẩn bị triển khai tương thích phiên bản ứng dụng cũ/mới theo expand → migrate/backfill → chuyển đọc/ghi → thu dọn khi an toàn. Đánh giá khóa, thời gian chạy, hiệu năng và quyền truy cập; có hướng dẫn dừng và giám sát.
4. Backup và diễn tập restore; ghi rõ migration nào đảo schema được, khi nào phải restore hoặc khắc phục tiến về phía trước. Rollback schema không hoàn tác dữ liệu hoặc email/thanh toán đã gửi. Commit Git chỉ quản lý mã, không phải phương án khôi phục database; không tự dùng `git restore` trên workspace có thay đổi của người dùng.


### 7.3 Chuyển quyền ghi và worker giữa phiên bản

Với mỗi use case có trạng thái dùng chung, tài liệu chuyển đổi phải chỉ rõ: nguồn ghi duy nhất theo giai đoạn; điều kiện chuyển hướng và tắt xử lý cũ; quyền sở hữu job/lease và cách tránh hai phiên bản cùng xử lý; contract event/webhook và quy tắc replay; dữ liệu cần backfill, checkpoint và cách chạy lại an toàn; đối soát trước/sau chuyển; điều kiện dừng và người xử lý. Tính toán riêng cho thanh toán–đơn–tồn: kiểm tra P-STOCK và trạng thái provider, không tự replay thanh toán hay xóa dấu vết khi chưa xác minh kết quả bên ngoài.

Trước chuyển: so schema/app cũ–mới, xác nhận backup đã có thể restore trên môi trường thử và thử failure injection. Trong chuyển: quan sát lỗi/queue, số bản ghi đối soát và quyền truy cập; nếu vượt ngưỡng đã thống nhất thì dừng chuyển giao, giữ bằng chứng và áp dụng runbook. Sau chuyển: xác nhận callers và worker cũ đã được xử lý, tiếp tục đối soát trước khi xóa adapter/schema cũ. Không giả định feature flag, rollback DB hay exactly-once có sẵn; nêu lựa chọn cụ thể và kiểm thử ở từng module.

## 8. Kế hoạch triển khai và cổng nghiệm thu

### Giai đoạn 0 — Baseline và ưu tiên rủi ro

- Ghi phiên bản môi trường, trạng thái repo và migration, kết quả build/test hiện tại, danh mục module và hợp đồng API. Không dùng số test cũ làm bằng chứng cho code mới.
- Thực hiện và ghi kết quả Q-SEC, Q-VER, Q-MAT, Q-TOKEN-LOG, P-STOCK, PLATFORM-RATE và DB-SCHEMA theo mục 3.5; ưu tiên phần liên quan trước khi chuyển use case hoặc phát hành. Lập ma trận mục 4 và tập hợp điểm mở nghiệp vụ; không yêu cầu chặn công việc độc lập khi thiếu môi trường, nhưng không đánh dấu gate liên quan đã đạt.

### Giai đoạn 1 — Pilot báo giá

- Chốt invariant, quyền, transaction, idempotency, version và phạm vi outbox. Thiết kế migration, rollout tương thích và khôi phục. Tách use case từng phần vào Application/Domain/Infrastructure; bảo toàn luồng diagnosis → quotation và chiết khấu quan hệ.
- Kiểm thử V1, clone/thay thế và báo giá bổ sung theo quy tắc đã xác nhận; giảm giá, bảo hiểm, kho, thanh toán, public link/OTP và quyền. Không mặc định mọi phiên bản cộng dồn.
- Dùng SQL Server thật hoặc LocalDB để test translation, unique và đồng thời có đồng bộ; test hai actor, các kiểu xung đột, request trùng/khác payload, retry, mất ACK và kiểm tra DB sau rollback. Test crash worker trước/sau gửi, hết lease, retry, replay và delivery trùng. LocalDB không chứng minh toàn bộ hành vi Azure SQL; test phù hợp trên staging trước rollout.

### Giai đoạn 2 — Chuyển các module còn lại

- Theo danh mục mục 3.2, thực hiện từng module và từng màn hình với bộ lọc server; theo dõi danh sách đã/chưa chuyển, vấn đề còn lại và người phụ trách. Không bật global filter hoặc thay route đồng loạt khi chưa kiểm chứng.
- Mỗi module: contract tests API cũ/mới; xác thực/phân quyền; integration SQL; luồng frontend quan trọng và dữ liệu lớn; quan sát hiệu năng; migration và rollback nếu thay schema. Có thể dùng feature flag hoặc phát hành từng nhóm khi cơ sở hạ tầng hỗ trợ.

### Giai đoạn 3 — Nghiệm thu và phát hành

Các cổng là **điều kiện cần**, không phải bảo đảm tuyệt đối. Với mỗi module ghi chủ xử lý/kiểm duyệt, câu lệnh hoặc CI job, commit, môi trường, ngưỡng và kết quả pass/fail/skip; tự động hóa dần thành CI check áp dụng cho phạm vi liên quan. Trường hợp môi trường chưa có, ghi chưa kiểm chứng và giữ gate phát hành tương ứng ở trạng thái chưa đạt.

1. `powershell scripts/check-transaction-policy.ps1` và rà soát transaction thực tế.
2. Build backend solution, frontend và `dotnet publish`; kiểm tra quy tắc phụ thuộc, DI, cấu hình, khởi động, health và hosted workers. Build/publish Docker trong môi trường có Docker, bao gồm restore các project reference; ghi chưa kiểm chứng nếu thiếu môi trường, không coi build local là bằng chứng image chạy.
3. Test tự động: unit + HTTP contract/quyền + integration SQL và nghiệp vụ, ghi rõ bài đã chạy/bỏ qua và môi trường. Dữ liệu/tình huống sản xuất cần kiểm thử staging phù hợp.
4. Kiểm tra migration/snapshot, tạo DB mới, upgrade dữ liệu đại diện, backup và diễn tập phục hồi. `has-pending-model-changes` không chứng minh DB thực tế không drift.
5. `npm run verify`, kiểm tra UTF-8 và test frontend/E2E cho các luồng quan trọng; kiểm chứng từng màn hình/bộ lọc đã chuyển, không suy ra toàn bộ màn hình an toàn từ build.
6. Trước khi chuyển mỗi module, thống nhất dữ liệu đại diện và ngưỡng theo yêu cầu cho latency, CPU/IO, số truy vấn, payload, đồng thời và tỷ lệ lỗi; ghi baseline và so kết quả, không tự đặt số giả. Kiểm tra execution plan, log, audit, cảnh báo, backlog outbox, tương thích hai phiên bản và kịch bản khôi phục.

Chỉ phát hành phần đạt nghiệm thu; ghi các giới hạn, kết quả đo và phần còn lại. Khi có sự cố, dừng rollout theo runbook, bảo vệ dữ liệu và quyết định khôi phục ứng dụng, tiến tới migration sửa hoặc restore theo tình huống. Không tuyên bố hoàn thành toàn dự án khi còn module hoặc điều kiện chưa kiểm chứng.
