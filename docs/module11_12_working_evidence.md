# Module 11–12 — tiến độ độc lập

Trạng thái: **đang chuyển, chưa nghiệm thu**. Không chuyển schema hay nguồn ghi dùng chung với module 9–10.

Cập nhật kiểm chứng: bài HTTP OData AI knowledge đã chạy qua reader Infrastructure và LocalDB schema tối thiểu, kiểm tra lọc, đếm, sắp xếp và không trả nội dung/URL. Chưa xác minh schema production. Chưa tìm thấy màn hình quản trị knowledge/model config hiện hữu trong frontend; không suy ra cần tạo màn hình mới từ việc chưa có caller.

Cập nhật module 12: hợp đồng quét tệp đã chuyển sang Application, các adapter sang Infrastructure, giữ namespace và DI. Kiểm thử DI và chữ ký tệp đã chạy; đây không phải kiểm chứng antivirus hay toàn bộ các luồng file/storage.

| Lát cắt | Bằng chứng | Điểm còn mở |
|---|---|---|
| AI forecast `GET /api/ai-forecasts` | [CODE] Chuyển adapter danh sách sang Infrastructure read-only; không gửi page giữ danh sách legacy, gửi page thì có header và giới hạn server. [TEST] HTTP cũ dùng adapter Web trong fixture; adapter SQL mới đạt trên schema tối thiểu trong project test tạm. | [OPEN] Customer portal và worker còn đọc trực tiếp bảng; chưa chuyển caller/nguồn ghi, chưa kiểm quyền chi nhánh hoặc benchmark. Đây là REST. |
| AI model config `GET /api/ai-model-configs/page` | [CODE] Endpoint đọc mới chỉ admin, Application + adapter Infrastructure dùng context chỉ đọc, không trả API key. [TEST] HTTP giả reader và LocalDB schema tối thiểu. | [OPEN] Chưa tích hợp frontend, chưa kiểm schema/migration nguyên trạng hay DI HTTP production; các route cũ giữ nguyên. Đây là REST. |
| AI knowledge `GET /api/ai-knowledge-bases/page` | [CODE] Endpoint phân trang admin qua Application và Infrastructure read-only; nguồn ghi và đường đọc chatbot cũ giữ nguyên. [TEST] HTTP giả reader và LocalDB schema tối thiểu. | [OPEN] Chưa tích hợp frontend, chưa kiểm schema/migration nguyên trạng, DI HTTP production; là REST. |
| AI dispatch `GET /api/ai-smart-dispatchings` | [CODE] Danh sách qua Application và Infrastructure read-only; hỗ trợ phân trang tùy chọn, không thay endpoint gợi ý cho xưởng. [TEST] SQL schema tối thiểu kiểm tra thứ tự và trang. | [OPEN] Scope Staff toàn chi nhánh kế thừa legacy, chưa nghiệm thu quyền; chưa kiểm schema nguyên trạng hay caller UI. Là REST. |
| Audit admin `GET /api/audit-logs/admin-changes`, `/entries` | [CODE] Chuyển adapter sang Infrastructure read-only; giữ quyền admin, thêm Id để thứ tự ổn định khi timestamp trùng; chuyển helper sửa UTF-8 sang Infrastructure giữ namespace. [TEST] HTTP InMemory trước chuyển dùng adapter Web; SQL so sánh hai adapter đạt trong bản sao tạm có workaround module 9. | [OPEN] Ghi audit và caller module 10 giữ nguyên; chưa kiểm runtime workspace, thay đổi assembly helper hoặc toàn bộ quyền. |
| OData AI knowledge metadata `/odata/AiKnowledgeCatalog` | [CODE] Endpoint mới admin-only, EDM và adapter Infrastructure chỉ project metadata. [TEST] HTTP và SQL đạt trong bản sao tạm có workaround module 9; SQL độc lập đạt trên schema tối thiểu. | [OPEN] Chưa kiểm pipeline workspace thực tế, schema nguyên trạng và frontend; không trả nội dung hoặc URL nguồn. |
| Gate pass `GET /api/gate-passes` | [CODE] Đọc qua Application/Infrastructure read-only, giữ paging và nguồn ghi cũ. [TEST] HTTP giả reader và LocalDB schema tối thiểu. | [OPEN] Scope Staff toàn chi nhánh giữ legacy, chưa xác minh quyền; endpoint REST, chưa kiểm schema nguyên trạng hoặc luồng giao dịch ghi. |
| Approval `GET /api/approval-requests` | [CODE] Đọc qua Application/Infrastructure read-only, giữ paging/DTO và đường ghi legacy. [TEST] SQL schema tối thiểu đạt trong project độc lập; HTTP/SQL đạt trong bản sao có workaround module 9. | [OPEN] HTTP workspace chưa chạy, Staff toàn chi nhánh là scope legacy chưa nghiệm thu, REST. |
| Gợi ý bay | [CODE] Lọc chi nhánh/trạng thái và lấy tối đa ba bay ở server qua port; adapter đọc Infrastructure, tie-break Id khi cùng SequenceOrder. [TEST] LocalDB schema tối thiểu so sánh adapter cũ/mới; HTTP dùng adapter Web trong fixture. | [OPEN] Đây là thay đổi hành vi tie-break, cần kiểm caller xưởng và scope trước nghiệm thu; context đọc có kết nối riêng. |

Chatbot và worker còn sử dụng trực tiếp dữ liệu AI; chat/notification cũng sử dụng audit. Vì vậy không đánh dấu toàn bộ module 11 hoặc 12 độc lập hay hoàn tất khi các lát cắt trên qua kiểm thử.

Rà lại hiệu năng: reader forecast/dispatch không còn phát lệnh COUNT bổ sung khi caller legacy yêu cầu toàn bộ danh sách; kiểm thử SQL schema tối thiểu kiểm tra số lệnh. Approval đã có kiểm thử HTTP qua reader Infrastructure và SQL thực trên schema tối thiểu, chưa xác nhận phạm vi chi nhánh hoặc schema production.

Lượt kiểm tra gần nhất: build solution trong workspace thành công sau thay đổi module 9; 298/298 kiểm thử backend đạt với LocalDB bật. Một số ca dùng schema tối thiểu/điều chỉnh hoặc reader giả, không xác nhận schema production hay toàn bộ module đã nghiệm thu.

Kiểm thử mở rộng trong bản sao tạm có workaround module 9: lượt đầy đủ 275/277 đạt, hai ca thất bại do bản sao thiếu `.env.example`; sau khi sao chép tệp mẫu, chạy lại riêng hai ca đạt 2/2. Đây không phải một lượt đầy đủ 277/277, không xác nhận source workspace hiện tại hay schema production.
