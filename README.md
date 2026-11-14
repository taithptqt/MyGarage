# MyGarage

## Tình trạng và hướng dẫn cho người tiếp nhận / AI

Hệ thống **đang chuyển đổi**, chưa được nghiệm thu hoàn chỉnh theo Clean Architecture, OData/REST và SOLID; frontend cũng chưa được xác nhận đã chuyển hết các bộ lọc và phân trang sang server. Không coi việc có bốn project, build thành công hoặc báo cáo cũ là bằng chứng hoàn tất.

Trước khi tiếp tục triển khai, đọc theo thứ tự:

1. [Quy tắc repository](AGENTS.md): các bất biến bắt buộc, UTF-8 và kỷ luật làm việc.
2. [Hiện trạng và chuẩn kiến trúc](docs/PROJECT_STATUS_AND_ARCHITECTURE.md): ảnh chụp, quy tắc phụ thuộc, API, frontend, an toàn dữ liệu và tiêu chí nghiệm thu.
3. [Công việc theo module](docs/PRODUCT_COMPLETION_MODULES.md): backlog chức năng và các việc cần kiểm tra trước khi đánh dấu hoàn tất.
4. [Master Blueprint](docs/mygarage_clean_architecture_odata_plan.md): kế hoạch và các điều kiện triển khai chi tiết.

Đối chiếu tài liệu với code, trạng thái Git và bằng chứng kiểm thử của revision đang làm; tài liệu là ảnh chụp có thể đã cũ. Ghi rõ phạm vi đã kiểm tra, bài test bị bỏ qua, môi trường và điểm còn mở. Không tự khẳng định production-ready hoặc xóa/thay thế thay đổi có sẵn khi chưa kiểm tra.
