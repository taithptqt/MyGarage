ARG DOTNET_VERSION=8.0
ARG NODE_VERSION=20-alpine
ARG BUILD_CONFIGURATION=Release

FROM node:${NODE_VERSION} AS frontend-deps
WORKDIR /src/frontend
COPY Frontend/package*.json ./
RUN npm ci

FROM frontend-deps AS frontend-build
WORKDIR /src/frontend
ARG VITE_GOOGLE_CLIENT_ID
ENV VITE_GOOGLE_CLIENT_ID=${VITE_GOOGLE_CLIENT_ID}
COPY Frontend/ ./
RUN test -n "$VITE_GOOGLE_CLIENT_ID" || (echo "Missing VITE_GOOGLE_CLIENT_ID at build time" && exit 1)
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS backend-restore
WORKDIR /src/backend
COPY backend/MyGarage.csproj ./
RUN dotnet restore MyGarage.csproj

FROM backend-restore AS backend-publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src/backend
COPY backend/ ./
RUN dotnet publish MyGarage.csproj -c ${BUILD_CONFIGURATION} -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=backend-publish /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./frontend/dist

USER app
ENTRYPOINT ["dotnet", "MyGarage.dll"]

