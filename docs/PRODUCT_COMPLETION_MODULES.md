# MyGarage — công việc hoàn chỉnh sản phẩm theo module

Mốc lập: **14/11/2026**. Danh sách này là backlog thực thi theo nhóm chức năng, không khẳng định các module đã hoàn tất. Chuẩn chung và trạng thái: [PROJECT_STATUS_AND_ARCHITECTURE.md](PROJECT_STATUS_AND_ARCHITECTURE.md); kế hoạch chi tiết: [Master Blueprint](mygarage_clean_architecture_odata_plan.md).

## Cách sử dụng

- Trước khi làm một module: kiểm kê route, màn hình/tab, actor, dữ liệu, job/hub, phụ thuộc, nguồn ghi, contract, test và migration hiện tại; ghi revision và chủ xử lý. Các mục dưới đây là điểm bắt đầu, không phải inventory đầy đủ.
- Trạng thái mặc định của mọi mục chưa có bằng chứng theo revision hiện tại: **cần đối chiếu/chưa nghiệm thu**. Không đánh dấu `[x]` chỉ vì có file, tên class hoặc báo cáo cũ; mỗi mục hoàn thành phải dẫn đến code, test, môi trường và giới hạn.
- Có thể khảo sát/làm phần độc lập song song; không cutover nguồn ghi hoặc phát hành khi phụ thuộc và cổng bắt buộc chưa đạt. Rà tích hợp sau mỗi lát cắt. Chỉ tạm hoãn lỗi nghiệp vụ không nghiêm trọng khi ghi rõ tác động và không cản trở điều kiện an toàn.

## 0. Nền tảng, xác thực và kiến trúc

- [ ] Kiểm kê project references, dependency thực tế, DI/composition, startup, worker/hub, route và caller; sửa vi phạm ranh giới Domain/Application/Infrastructure/API theo use case.
- [ ] Rà session, token, CORS, rate limit, lỗi/log, bí mật, branch/ownership và quyền từng actor; test token hết hạn/thu hồi, trùng ID giữa loại tài khoản và caller cũ.
- [ ] Kiểm thử cấu hình OData, giới hạn query, an toàn dữ liệu, REST compatibility, OpenAPI, container/CI và cấu hình giấy phép phụ thuộc.

## 1. Chẩn đoán, báo giá và lệnh sửa chữa

- [ ] Kiểm kê và chuyển từng luồng diagnosis → Quotation → Service Order; không tạo liên kết trực tiếp diagnosis–SO; giữ chiết khấu quan hệ là nguồn tính duy nhất.
- [ ] Kiểm tra phiên bản, clone/thay thế/phát sinh, bảo hiểm, public token/OTP, sở hữu/chi nhánh, kho và cạnh tranh cập nhật; không giả định mọi V2 cùng nghiệp vụ.
- [ ] Kiểm thử HTTP/SQL, retry, lỗi giữa các bước, tương thích màn hình và đối soát liên module trước cutover.

## 2. Đặt lịch và tiếp nhận

- [ ] Chuyển/làm rõ các luồng đặt lịch, nhóm lịch, slot, trạng thái, timezone, thông báo và phân quyền; kiểm tra lặp/đồng thời và các ngày biên.
- [ ] Kiểm tra `MyAppointmentsPage`, `ReceptionAppointmentsPage`, bộ lọc/tổng và nhóm: phân trang server đúng đơn vị nhóm, tránh tải toàn bộ và gọi chi tiết từng nhóm khi không cần.

## 3. Xưởng, điều phối, QC và bàn giao

- [ ] Rà phân công/điều chuyển, xung đột, trạng thái QC, bàn giao, realtime và quyền từng vai trò; kiểm tra dữ liệu kho và lệnh liên quan.
- [ ] Kiểm kê, chuyển bộ lọc/phân trang có dữ liệu lớn của `QCDashboard`, `ReceptionOrdersSection` và các bảng liên quan; bảo toàn nhóm, tổng số và export.

## 4. Kho, vật tư và vị trí

- [ ] Rà tồn khả dụng/đặt trước/xuất/trả/kiểm kê, lịch sử, nhiều kho/chi nhánh, yêu cầu vật tư và bay; chuyển adapter theo ranh giới kiến trúc.
- [ ] Xác định mọi writer cùng tồn kho, điều phối đồng thời và transaction; kiểm thử nhiều luồng, rollback, dữ liệu âm và đối soát trên SQL/schema mục tiêu.
- [ ] Rà từng bảng/tab, thống kê, tìm kiếm, phân trang server, export và quyền.

## 5. Mua hàng, nhà cung cấp và nhập hàng

- [ ] Kiểm tra vòng đời đơn mua, duyệt/hủy, nhập một phần/nhiều lần, nhà cung cấp, giá vốn và đối soát kho.
- [ ] Xác định idempotency và cạnh tranh nhận hàng, caller cũ, transaction, migration và phục hồi khi lỗi; kiểm thử SQL và UI theo contract.

## 6. Bán lẻ, giỏ hàng và voucher

- [ ] Rà giá/thuế/chiết khấu quan hệ, giỏ hàng, đơn, trả hàng, thanh toán–tồn và phạm vi chi nhánh; xác nhận quy tắc hoàn tiền trước khi thay đổi.
- [ ] Kiểm tra đồng bộ tồn với mọi writer, retry/commit không rõ kết quả, giao dịch ngoài và callback; không suy ra an toàn toàn hệ thống từ khóa riêng bán lẻ.
- [ ] Đối chiếu từng tab voucher/campaign/approval/redemption/report/export; xử lý membership lớn, phân quyền, đồng thời và phản hồi lỗi/response cũ.

## 7. Thanh toán, hóa đơn, công nợ và đối soát

- [ ] Rà PayOS/provider, chữ ký, webhook lặp/sai thứ tự, thanh toán một phần, hoàn/hủy theo nghiệp vụ đã chốt, công nợ và sổ cái.
- [ ] Kiểm tra phạm vi hóa đơn/tổng thuế, server filtering/page/count/export, đối chiếu tổng, transaction và tính nhất quán khi dữ liệu đổi.
- [ ] Thiết kế đối soát, retry/replay an toàn và kiểm thử liên module thanh toán–đơn–tồn; không giả định có thể rollback provider.

## 8. Khách hàng, xe và danh mục quản trị

- [ ] Rà CRUD khách hàng, ownership xe/chuyển chủ, branch, lịch sử và dữ liệu cũ; kiểm tra quyền actor cùng/khác chi nhánh, liên kết đã xóa và chủ cũ.
- [ ] Chuyển các danh mục xe/dịch vụ/phụ tùng theo use case; kiểm tra adapter Infrastructure, lookup/modal, filter/sort/page, giá và tương thích client.
- [ ] Xác nhận ràng buộc biển số/VIN, concurrency, dữ liệu lịch sử và migration trên schema thực.

## 9. Nhân sự, lương và kế toán

- [ ] Rà hồ sơ/yêu cầu lương, kỳ lương, điều chỉnh, hoa hồng, payslip, quyền nhạy cảm, audit và phạm vi chi nhánh; di chuyển truy vấn/logic còn trong API theo use case.
- [ ] Kiểm thử khóa kỳ cạnh tranh tạo/tính bảng lương; đối chiếu tổng và phân trang, báo cáo/export, dữ liệu nhân viên tham chiếu và UI.

## 10. Cổng khách hàng, chat và thông báo

- [ ] Kiểm kê controller/caller thực tế, loại bỏ hoặc hoàn thiện service mới chưa dùng; không chuyển nguồn ghi trước khi đối chiếu contract và quyền.
- [ ] Kiểm tra xe, trạng thái lệnh, public link, nhóm SignalR, file, notification, reconnect/delivery trùng và dữ liệu cá nhân.
- [ ] Với service xe mới: kiểm chứng schema/SQL, parity, validation, ownership cạnh tranh, retry/context sạch, commit không rõ và transaction ngoài trước khi nối controller.

## 11. AI, dashboard, báo cáo và nội dung

- [ ] Kiểm kê forecast, knowledge, model config, dispatch, chatbot, báo cáo và home/news/contact, gồm worker/caller không có màn hình riêng; xác định nguồn ghi và quyền.
- [ ] Kiểm tra cấu hình/bí mật, timeout, chi phí, fallback, filter/page/export và số liệu báo cáo; chỉ tạo/chuyển màn hình khi có nhu cầu/caller được xác nhận.
- [ ] Đối chiếu reader Infrastructure, schema/migration, OData/REST và pipeline thật; benchmark khi dữ liệu đại diện sẵn có.

## 12. Thành phần dùng chung và tích hợp chưa gán

- [ ] Kiểm kê search, file/storage, gate pass, approval, audit, background jobs và API/worker/hub ngoài danh sách; xác định chủ sở hữu và caller.
- [ ] Kiểm tra quyền, quét tệp và lưu trữ, retention/PII, retry/lease/idempotency, lỗi/khôi phục và tích hợp ngoài. Kiểm tra magic bytes không tương đương antivirus.

## 13. Nghiệm thu sản phẩm và phát hành

- [ ] Chạy dependency/build, test backend/frontend, HTTP, SQL và migration theo revision; ghi rõ các test bị skip và giới hạn schema. Kiểm thử trình duyệt và các hành trình xuyên module.
- [ ] Đo hiệu năng với dữ liệu đại diện; kiểm tra bảo mật và quyền, tính đúng tổng/đối soát, sao lưu/khôi phục, khả năng quan sát và runbook vận hành.
- [ ] Đối chiếu migration/backfill trên môi trường mục tiêu; xác nhận giấy phép, dữ liệu lịch sử, rollout/rollback và chủ xử lý. Không coi checklist này là bằng chứng đã phát hành hay production-ready.

## Mẫu theo dõi từng lát cắt

| ID/module | Route hoặc màn hình/caller | Actor và scope | Cũ → mới / nguồn ghi | Trạng thái | Bằng chứng CODE/TEST/BUSINESS | Migration/rollback | Điểm mở / chủ xử lý |
|---|---|---|---|---|---|---|---|
| Điền khi thực hiện | | | | chưa khảo sát | revision, môi trường, lệnh, pass/fail/skip | | |
