# Stage 1: Build Frontend (React + Vite)
FROM node:20-alpine AS frontend-build
WORKDIR /src/frontend

# Copy package files and install dependencies
COPY frontend/package*.json ./
RUN npm ci

# Copy frontend code and build
COPY frontend/ .
# Bạn có thể bỏ qua quá trình check types hoặc test để build nhanh hơn nếu cần
RUN npm run build


# Stage 2: Build Backend (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src/backend

# Copy csproj and restore
COPY backend/MyGarage.csproj ./
RUN dotnet restore

# Copy backend code and publish
COPY backend/ .
RUN dotnet publish -c Release -o /app/publish


# Stage 3: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Chuyển server lắng nghe port 8080 thay vì 7009
EXPOSE 8080

# Thiết lập môi trường Production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

# Copy published backend files (Thực thi API)
COPY --from=backend-build /app/publish .

# Copy frontend build (HTML/JS/CSS) bỏ vào đúng cấu trúc "/app/frontend/dist"
# Điều này giúp hàm ResolveFrontendDistPath trong Program.cs tự động tìm thấy frontend!
COPY --from=frontend-build /src/frontend/dist ./frontend/dist

# Entry point để chạy MyGarage
ENTRYPOINT ["dotnet", "MyGarage.dll"]
