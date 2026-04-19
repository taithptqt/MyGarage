# Kiến trúc Frontend (Chuẩn hóa)

Mục tiêu:
- T�ch r� ph?n `app`, `features`, `shared`, `legacy`.
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
  - Kh�ng th�m logic moi v�o d�y.

## Quy tắc import

- Ưu tiên import theo lơp:
  - `app` -> `features`, `shared`.
  - `features` -> `shared` và nội bộ feature.
  - `shared` không phụ thuộc `features` (trừ trường hợp đã được thiết kế rõ ràng).
- Alias dùng cho tđ. chức kiến trúc:
  - `@app`, `@features`, `@shared`, `@legacy`, `@context`.

## Tương thích ngược

- C�c thu m?c cu `context`, `hooks`, `utils`, `components/auth`, `styles` du?c gi? b?ng file forwarder.
- Kh�ng x�a file cu ? giai do?n chuy?n ti?p d? tr�nh v? import.






