# MyGarage — hiện trạng và chuẩn kiến trúc

Mốc khảo sát: **14/11/2026**, trên workspace đang có thay đổi chưa commit. Tài liệu này là ảnh chụp để phối hợp triển khai, không phải chứng nhận production hay báo cáo kiểm thử cho một commit. Cập nhật lại ngày, revision và bằng chứng sau mỗi đợt thay đổi.

Nguồn kế hoạch: [Master Blueprint](mygarage_clean_architecture_odata_plan.md). Danh sách công việc: [PRODUCT_COMPLETION_MODULES.md](PRODUCT_COMPLETION_MODULES.md). Quy tắc `AGENTS.md` ở gốc repository là bắt buộc; khi có khác biệt, đối chiếu mã hiện tại, yêu cầu người dùng và quyết định đã được phê duyệt trước khi thay đổi tài liệu hoặc hành vi.

## 1. Kết luận hiện tại

**Đang chuyển đổi; chưa có cơ sở tuyên bố hoàn tất Clean Architecture, OData/REST, SOLID hoặc toàn bộ frontend.** Đã có các project `backend/src/MyGarage.Api`, `MyGarage.Application`, `MyGarage.Domain`, `MyGarage.Infrastructure` và có các endpoint OData, REST cùng nhiều màn hình đã dùng phân trang server. Cấu trúc thư mục và việc build thành công không tự chứng minh mọi use case tuân thủ kiến trúc hoặc hoạt động đúng.

| Phạm vi | Bằng chứng mã đã khảo sát | Chưa đủ bằng chứng |
|---|---|---|
| Backend | Có bốn project; `SalaryAdminController` vẫn có truy vấn SQL; service cổng khách hàng và thông báo mới được đăng ký DI nhưng chưa thấy caller runtime trong phạm vi tìm kiếm. | Chưa kiểm kê đầy đủ dependency/use case và chưa chứng minh mọi lớp đáp ứng quy tắc phụ thuộc, SOLID hoặc hợp đồng cũ. |
| API | OData được cấu hình; có các endpoint REST/OData và route tương thích. | Chưa nghiệm thu toàn bộ route, quyền, contract, OData limits, schema và caller. REST hợp lệ là lựa chọn phù hợp với nhiều use case. |
| Frontend | Một số danh sách chuyển sang lọc/phân trang server. `QCDashboard` và `ReceptionOrdersSection` còn chia trang client; lịch hẹn khách hàng còn tải chi tiết theo từng nhóm. | Chưa xác nhận mọi màn hình, tab, bộ lọc, tổng số, export và tương tác trình duyệt; chưa benchmark dữ liệu đại diện. |
| Dữ liệu và phát hành | Có migration, test InMemory và LocalDB cho một số lát cắt. | Cần xác nhận migration, dữ liệu lịch sử, schema/index, quyền, backup/khôi phục và điều kiện vận hành trên môi trường mục tiêu. |

Các evidence module 6–12 trong `docs/` ghi nhận lịch sử kiểm thử và giới hạn ở thời điểm viết; một số đường dẫn hoặc chi tiết đã cũ sau khi di chuyển project. Đối chiếu workspace/commit hiện tại trước khi dựa vào chúng. Các kết quả test cũ không thay thế lượt chạy trên revision mới; test SQL bị bỏ qua, schema tối thiểu hoặc có điều chỉnh phải được ghi rõ.

## 2. Quy tắc kiến trúc bắt buộc

1. **Phụ thuộc hướng vào trong:** Domain không phụ thuộc API, Application hoặc Infrastructure; Application phụ thuộc Domain và định nghĩa use case/port; Infrastructure triển khai persistence, tích hợp và worker thông qua contract hướng vào trong; API xử lý HTTP, xác thực, composition và gọi use case. Kiểm tra project references **và mã thực thi**, không chỉ namespace hoặc vị trí file.
2. **Tách theo use case:** controller không chứa truy vấn và điều phối nghiệp vụ mới; quy tắc miền đã xác nhận nằm trong Domain, orchestration/authorization theo tài nguyên nằm trong Application, EF/SQL và dịch vụ bên ngoài nằm trong Infrastructure. Không ép mọi thao tác qua generic repository hoặc tạo interface chỉ để tăng số lớp.
3. **SOLID có thể kiểm chứng:** mỗi thành phần có trách nhiệm rõ; thay thế implementation không đổi hợp đồng; interface vừa đủ cho caller; phụ thuộc vào abstraction tại ranh giới cần thiết. Kiểm tra bằng review, test và dependency graph, không tuyên bố đạt chỉ vì có DI.
4. **Một nguồn ghi theo từng giai đoạn:** kiểm kê controller, worker, hub và client cũ/mới; giữ route tương thích có kiểm thử, quy định nguồn ghi và điều kiện cutover. Service mới không có caller chưa phải use case đã chuyển. Không chuyển caller khi transaction, quyền và hợp đồng của nó chưa đạt yêu cầu tương ứng.
5. **Bất biến miền:** khuyến nghị chẩn đoán phải đi qua Quotation, không liên kết trực tiếp SO; chiết khấu quan hệ là nguồn chân lý, không dùng cột `TotalDiscount` legacy làm nguồn nghiệp vụ. Không tự đặt quy tắc mới khi chưa xác minh hành vi/yêu cầu.
6. **Quyền và dữ liệu:** xác thực và kiểm tra actor, role, quyền sở hữu và phạm vi chi nhánh ở server trước truy vấn/ghi; kiểm thử actor hợp lệ và sai phạm vi, kể cả ID staff/customer trùng. Không trả trường nhạy cảm ngoài hợp đồng; bảo vệ token/PII trong log.
7. **Giao dịch và tích hợp:** định nghĩa ranh giới transaction, owner của transaction ngoài, idempotency, concurrency, xử lý rollback và commit không rõ kết quả. Không nuốt lỗi rồi tiếp tục ghi; không mặc định retry an toàn hoặc tác vụ ngoài rollback cùng DB. Kiểm thử các writer cùng dữ liệu và tình huống webhook/job lặp hoặc sai thứ tự khi áp dụng.
8. **REST và OData:** giữ contract route/verb/status/DTO đã chốt; bảo đảm fallback không che lỗi phân quyền/lỗi server. OData chỉ dùng khi phù hợp; giới hạn query, scope trước filter/page, giới hạn `$top`/độ phức tạp và kiểm thử SQL translation. Không bắt buộc mọi endpoint dùng OData.
9. **Frontend và dữ liệu lớn:** bộ lọc, sort ổn định, đếm và phân trang nghiệp vụ thực hiện tại server khi dữ liệu có thể lớn; phân trang theo đơn vị nhóm nếu màn hình hiển thị nhóm. Kiểm tra reset trang, race response, loading/error, tổng toàn bộ tập lọc, CRUD refresh và export. Không coi gom tất cả trang hoặc chỉ đổi URL là tối ưu dữ liệu lớn.
10. **UTF-8 và UI:** giữ nguyên tiếng Việt khi không được yêu cầu đổi; tách nội dung hướng dẫn khỏi xử lý dữ liệu. Sau sửa JSX/CSS/JS kiểm tra diff, đọc lại đoạn sửa và chạy kiểm tra encoding theo `AGENTS.md`.
11. **Schema và triển khai:** kiểm tra migration/model snapshot, dữ liệu lịch sử, index, khả năng backfill/replay, backup và phương án dừng/khôi phục. Không tự chạy migration lên production chỉ vì test local đạt. Xác nhận giấy phép/cấu hình AutoMapper theo môi trường thực tế trước rollout.

## 3. Quy trình và cổng nghiệm thu

Mỗi lát cắt phải có danh mục route/trang/tab/caller/job, actor và scope, hợp đồng cũ/mới, dữ liệu/transaction, migration và rollback; phân biệt `[CODE]`, `[TEST]`, `[BUSINESS]` và `[OPEN]`. Ghi chủ xử lý, revision, môi trường, câu lệnh, số pass/fail/skip, giới hạn và ngày. Trạng thái dùng `chưa khảo sát`, `đang làm`, `cần xác nhận`, `đã kiểm thử`, `đã nghiệm thu`.

Trước khi đánh dấu **đã nghiệm thu**: (1) mapping kiến trúc và dependency đạt; (2) build/kiểm thử unit, API và frontend phù hợp đạt; (3) kiểm thử SQL thật, migration/schema và concurrency khi liên quan, ghi rõ mọi skip; (4) quyền, hợp đồng, tác động xuyên module và hành vi UI được kiểm chứng; (5) có bằng chứng hiệu năng, rollout/rollback và các quyết định nghiệp vụ cho phạm vi phát hành. Nếu chưa có môi trường hoặc bằng chứng, giữ trạng thái chưa nghiệm thu và mô tả giới hạn.

Các lỗi nghiệp vụ không nghiêm trọng có thể được ghi backlog để tiếp tục chuyển module theo chỉ đạo hiện tại; không coi lỗi bảo mật, mất dữ liệu, transaction hoặc điều kiện bắt buộc cho cutover là đã xử lý. Chỉ đánh dấu hoàn tất sản phẩm sau khi đóng các hạng mục áp dụng và nghiệm thu tích hợp/triển khai thực tế.
