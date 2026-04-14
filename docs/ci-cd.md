# CI/CD cho MyGarage

## Tổng quan

Repo đã có sẵn 2 workflow GitHub Actions:

- `CI`: `.github/workflows/ci.yml`
- `CD`: `.github/workflows/cd.yml`

`CI` sẽ chạy khi push/PR vào `main`, `develop`:

- Build backend `.NET 8`
- Build frontend `Vite` (Node 20)
- Build Docker image (không push registry)

`CD` sẽ deploy production:

- Tự chạy khi workflow `CI` của branch `main` thành công
- Hoặc chạy tay qua `workflow_dispatch`
- Deploy qua SSH lên server và chạy `docker compose up -d --build`

## Secrets cần cấu hình

Vào GitHub repo `Settings > Secrets and variables > Actions`, thêm:

- `SSH_HOST`: IP/domain server deploy
- `SSH_USERNAME`: user SSH trên server
- `SSH_KEY`: private key (PEM/OpenSSH) tương ứng public key đã add vào server
- `SSH_PORT`: port SSH (thường `22`)
- `DEPLOY_PATH`: đường dẫn repo trên server, ví dụ `/opt/mygarage`

## Chuẩn bị server 1 lần

Trên server production, đảm bảo:

1. Đã cài `git` và `docker` + `docker compose`.
2. Repo đã clone vào `DEPLOY_PATH`.
3. File env runtime đã sẵn sàng (theo `docker-compose.yml` hiện tại đang đọc `Frontend/.env`).
4. User SSH có quyền chạy Docker.

## Luồng chạy sau khi bật

1. Push code lên `main`.
2. `CI` chạy build + verify.
3. Khi `CI` xanh, `CD` tự SSH vào server và deploy.

## Lưu ý

- Workflow đang deploy nhánh `main` cố định.
- Nếu bạn muốn staging/prod tách riêng, có thể thêm `cd-staging.yml` cho branch `develop`.
- Project hiện chưa có test project riêng, nên `dotnet test` được cấu hình tự bỏ qua khi không tìm thấy `*Tests*.csproj`.
