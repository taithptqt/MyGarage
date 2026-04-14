# Kiến trúc Frontend (Chuẩn hóa)

Mục tiêu:
- Tách rõ phần `app`, `features`, `shared`, `legacy`.
- Giảm coupling giữa module.
- Dđ. mở rộng theo nguyên tắc SOLID cho frontend.

## Cấu trúc chuẩn

- `app/`
  - Điểm vào ứng dụng, routing cấp cao, bootstrap.
- `features/`
  - Chia theo nghiệp vụ: `appointments`, `auth`, `workshop`, ...
  - Mđ-i feature tự quản `api`, `components`, `hooks`, `pages`, `utils`.
- `shared/`
  - Tài nguyên dùng chung toàn hệ thđng.
  - `shared/core/`: context, hooks, utils lõi, style nền.
  - `shared/components/`, `shared/layouts/`: UI dùng lại.
- `legacy/`
  - Mã Horizon cũ, giữ tương thích tạm thời.
  - Không thêm logic mơi vào đây.

## Quy tắc import

- Ưu tiên import theo lơp:
  - `app` -> `features`, `shared`.
  - `features` -> `shared` và nội bộ feature.
  - `shared` không phụ thuộc `features` (trừ trường hợp đã được thiết kế rõ ràng).
- Alias dùng cho tđ. chức kiến trúc:
  - `@app`, `@features`, `@shared`, `@legacy`, `@context`.

## Tương thích ngược

- Các thư mục cũ `context`, `hooks`, `utils`, `components/auth`, `styles` được giữ bằng file forwarder.
- Không xóa file cũ ở giai đoạn chuyển tiếp để tránh vỡ import.





