# Kết quả kiểm thử clone MY GARAGE

Ngày kiểm thử: 13/11/2026 (Asia/Saigon).

## Cập nhật giao diện tiếng Anh

Tiếng Anh hiện là mặc định tại `/en/`, các URL trang chính cũ chuyển sang bản tiếng Anh tương ứng. Đã thu 265 route tiếng Anh của build nguồn và lưu 2.704 bản dịch bổ sung cho nội dung còn tiếng Đan Mạch, gồm mô tả xe, metadata, các trang thông tin và chuỗi UI động. Tất cả mô tả xe trong các snapshot tiếng Anh đã có bản dịch. 12 schema form, tùy chọn dịch vụ, cookie, thông báo validation/demo và locale lịch đã chuyển sang tiếng Anh.

`npm test`: 14/14 pass; HTTP smoke test xác nhận root trả 302 → `/en/`, trang chủ EN trả 200 và form Storage trả Email/Phone/Car make/Model/Year. `english-audit.json` ghi nhận 268 snapshot được rà và không có các cụm tiếng Đan Mạch mục tiêu. Đây là kiểm tra tự động theo tập từ khóa, không phải chứng nhận kiểm duyệt mọi câu dịch. Công cụ trình duyệt không còn khả dụng ở cuối lượt thay đổi nên chưa có lượt visual QA mới cho bản tiếng Anh.

## Cập nhật sau sửa — 13/11/2026

Các lỗi xác định trong báo cáo bên dưới đã được xử lý. Phần còn lại của tài liệu giữ lại kết quả **trước khi sửa** để truy vết.

| Hạng mục | Trạng thái sau sửa |
| --- | --- |
| Storage Loading/502 | Đã sửa bằng schema local; 5 trường bắt buộc, 14 tùy chọn, kiểm tra email/required và hoàn tất demo đã thử trên trình duyệt. |
| Các form động khác | Đã thu cấu trúc từ form hiển thị trên nguồn và bổ sung đủ 12 schema: 4, 5, 7, 8, 13, 14, 20, 21, 22, 23, 24, 28. Form Sales đã được kiểm tra hiển thị và không có lỗi console. |
| Điều hướng Vue | Đã bỏ ép reload; tạo route JSON khớp hash/data từ HTML snapshot. Kiểm tra FAQ → Sales → Back và Home → Storage giữ nguyên document trên desktop/mobile. |
| Scroll khi đổi route | Đã sửa lỗi listener ScrollIndicator của build gốc: kiểm tra phần tử còn tồn tại và gỡ listener khi component bị hủy. Lượt kiểm thử tab mới không còn lỗi console sau Home → Storage → cuộn. |
| Anchor mobile | Đã thay scroll-to cũ bằng smooth scroll cho link cùng trang. Nút “Få din bil opbevaret” đưa đầu section `#anmod` về y=0, không reload. |
| Cookie | Đã có UI local với 4 nhóm, chi tiết, 3 lựa chọn đồng ý, lưu lựa chọn, mở lại và focus bàn phím. Đã kiểm tra chọn Statistik → lưu → mở lại vẫn checked; đã kiểm tra mobile 355px. Đây là bản dựng local, không phải dịch vụ Cookiebot thật. |
| Media được báo thiếu | 1 video Lotus và 20 ảnh đã có trong cache; video Lotus đủ 948.975.781 byte. Tải/phục vụ file lớn theo luồng, hỗ trợ seek/suffix range/HEAD/416. |
| `/mclaren-til-salg/` | Nguồn trả HTTP 404. Local giữ HTML 404 cùng giao diện và trả đúng 404, không đổi thành lỗi proxy 502. |
| Crawler | Đã loại URL template rác và favicon tương đối bị nhân bản; bỏ giới hạn cố định 300 trang. Có chế độ repair và MAX_PAGES tùy chọn. Chưa khẳng định đã crawl hết mọi URL của website. |

`npm test`: **10/10 pass**. Kiểm tra server chạy với `OFFLINE=1`: route JSON/HTML, 12 schema form, video range/HEAD, 404, chặn POST và patch ScrollIndicator/analytics đều đạt. Cache hiện khoảng 2,68 GB; xem `inventory.json` để biết số liệu mới nhất.

Bằng chứng sau sửa nằm trong `evidence/fixed/`: form Storage desktop/mobile, trạng thái demo hoàn tất, cookie desktop/mobile và form Sales. Form chỉ chạy demo local; không gửi dữ liệu tới công ty. Các search query chưa cache hoặc trang chưa thu thập vẫn cần mạng. Chưa thực hiện pixel-diff/frame-by-frame cho mọi trang và breakpoint.

## Báo cáo gốc trước sửa

Kết luận: **chưa đạt yêu cầu clone đầy đủ UI + animation + media**. Bản hiện tại là mirror build Vue đã biên dịch. Phần hiển thị được kiểm tra khá sát nguồn, nhưng có lỗi form, thay đổi điều hướng và chưa độc lập offline.

Không sửa code ứng dụng trong lượt kiểm thử này. Truy cập trình duyệt có thể bổ sung tài nguyên vào cache do cơ chế sẵn có của server.

## Phạm vi và kết quả

| Kiểm tra | Kết quả | Bằng chứng / giới hạn |
| --- | --- | --- |
| Video nền trang chủ | Đạt mẫu đã thử | Local video readyState=4, paused=false, thời gian phát tăng. |
| Font và tiêu đề desktop | Đạt mẫu đã thử | DINCondensed; tiêu đề h2 64px ở viewport desktop. |
| Storage desktop so với nguồn | Đạt về phép đo bố cục | Hai tab cùng 1280×720; chiều cao trang 6225px; vị trí các tiêu đề chính khớp: 810, 1899, 2393, 3658, 4023, 4815px. Không phải phép đo pixel-diff toàn trang. |
| Ảnh xe trang chủ / chi tiết | Đạt mẫu đã thử | Không thấy ảnh eager bị hỏng; trang Porsche Macan 4 có 61/61 thẻ img tải được trong lượt kiểm tra. Không suy rộng thành mọi ảnh toàn website. |
| Tìm xe bằng từ khóa | Đạt mẫu đã thử | Nhập Porsche vào danh sách xe làm thay đổi danh sách hiển thị. |
| Tìm kiếm toàn site | Đạt khi có mạng | Nhập Porsche nhận các nhóm Partners, Sider, Nyheder. Không chứng minh được offline cho mọi từ khóa. |
| Link danh sách → chi tiết xe | Đạt mẫu đã thử | Mở được `/cars/porsche-macan-4-768053987/` trên local. |
| Gallery xe | Đạt mẫu đã thử | Nút phải đổi transform từ translate3d(-576px,0,0) sang -1152px; transition 700ms. |
| Mở rộng thông số xe | Đạt | Se flere detaljer mở thêm Servicebog, Type, Hjultræk, kích thước; nút đổi thành Se færre detaljer. |
| Menu mobile | Đạt mẫu đã thử | Tab mobile 355×761 mở/đóng menu được; scrollWidth 340px, không thấy tràn ngang. Không kiểm tra đủ mọi breakpoint hoặc thiết bị thật. |
| Popup yêu cầu Storage | **Không đạt** | Bản gốc hiện form đầy đủ; local chỉ hiện Loading… và lỗi Axios 502. |
| Gửi form | Khác thiết kế nguồn | Local chặn submit, hiện thông báo preview; không gửi thử dữ liệu tới công ty. |
| Điều hướng / animation chuyển trang | **Không tương đương** | local-guard chặn click link nội bộ rồi gán location.href, ép full reload thay vì để Vue xử lý. |
| Cookie UI | **Thiếu** | Script Cookiebot bị loại khỏi HTML; không có bản tái hiện dialog/các trạng thái consent trong code local. |
| Offline toàn website | **Chưa đạt** | Server tải từ nguồn khi cache miss; crawl giới hạn 300 trang và bỏ wp-json. |

## Lỗi cần ưu tiên

### P1 — Form Storage bị kẹt Loading

Tái hiện: mở `/storage/` → bấm **Forespørg nu**.

- Local: chỉ có tiêu đề popup và Loading…; console báo Axios Request failed with status code 502.
- Nguồn: có Navn, Mail, Telefon, Bilens mærke, Model, Årgang và nhiều tùy chọn dịch vụ.
- Request local: `/_remote/mygarage.skaevt.com/wp-json/gf/v2/forms/8` trả 502 với nội dung `401 https://mygarage.skaevt.com/wp-json/gf/v2/forms/8`.
- Gọi upstream không có xác thực trả 401. Trong server.mjs, acquire chỉ gọi fetch(url,{signal}) và không chuyển tiếp headers từ request trình duyệt. Đây là nguyên nhân phù hợp với khác biệt quan sát được; chưa kiểm tra nội dung thông tin xác thực để tránh đưa dữ liệu đó vào báo cáo.
- Hướng xử lý: tái hiện schema/form UI local và các trạng thái validation/loading/success/error; không cần gửi dữ liệu thực để kiểm thử UI.

Ảnh bằng chứng: `evidence/retest/storage-form-local.jpg` và `storage-form-original.jpg`.

### P2 — Điều hướng làm mất tính tương đương animation

`server.mjs:47` chặn mọi link cùng origin ngoài `/assets/`, kể cả link hash, rồi gán location.href. Link khác trang luôn gây tải lại document. Vì vậy không thể coi việc dùng JS gốc là bảo đảm toàn bộ transition gốc đã được giữ nguyên. Cần bỏ việc ép reload và kiểm thử lại router, back/forward, hash scroll và chuyển trang.

### P2 — UI cookie chưa có

htmlClean loại script Cookiebot nhưng không thay bằng UI local. Đây là phần UI/trạng thái tương tác bị thiếu so với yêu cầu ban đầu.

### P2 — Chưa có kho media offline đầy đủ

Đối chiếu capture-report.json với file cache hiện có tại thời điểm kiểm tra:

- 2039 URL trong deferred vẫn chưa có file cache.
- 2016 là đường dẫn `/fav/` bị lặp theo các thư mục trang; không được diễn giải thành 2016 ảnh nội dung bị thiếu.
- 1 là URL rác trích từ template JavaScript: `/cars/${this.car.make.replace(/`.
- 1 là trang `/mclaren-til-salg/` chưa có cache.
- 21 là media nội dung: 1 video Lotus và 20 ảnh. Ví dụ video `Lotus-T132-Studio-B-Roll-Yellow.mp4`, ảnh `Rolls-Royce-Dawn-Black-SoMe-4.jpg`, `Rolls-Royce-Ghost-Green-8.jpg` và `Dennis-Stendorph-MY-GARAGE-1.jpg`.

Đây là kiểm kê những URL đã phát hiện, không phải toàn bộ asset website: crawler giới hạn 300 trang, loại wp-json và chưa có cơ chế bao phủ mọi trạng thái lazy-load. Cache miss còn có thể tải online; chưa có cache không tự động đồng nghĩa URL nguồn hỏng.

## Những điều chưa thể xác nhận

- Chưa có pixel-diff được chuẩn hóa cho toàn bộ trang / breakpoint. Các tab ban đầu có viewport khác nhau nên các phép đo đó đã được loại khỏi kết luận so sánh.
- Chưa đo timeline/easing/frame-by-frame cho mọi animation; kiểm tra gallery và menu chỉ là mẫu.
- Chưa kiểm thử mọi trang đối tác, bài viết, modal mua xe, chia sẻ, trạng thái lỗi/empty và toàn bộ form.
- Không gán phần trăm độ giống vì chưa có phép đo đủ bao phủ.

Ưu tiên tiếp theo: sửa form Storage → phục hồi luồng điều hướng Vue → bổ sung UI cookie → hoàn thiện crawler/media → chạy visual regression trên các template và breakpoint chính.
