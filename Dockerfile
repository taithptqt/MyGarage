## syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=8.0
ARG NODE_VERSION=20-alpine
ARG BUILD_CONFIGURATION=Release

FROM node:${NODE_VERSION} AS frontend-deps
WORKDIR /src/frontend
COPY Frontend/package.json Frontend/package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund

FROM frontend-deps AS frontend-build
WORKDIR /src/frontend
ARG VITE_GOOGLE_CLIENT_ID
ARG NODE_OPTIONS=--max-old-space-size=2048
ENV VITE_GOOGLE_CLIENT_ID=${VITE_GOOGLE_CLIENT_ID}
ENV NODE_OPTIONS=${NODE_OPTIONS}
COPY Frontend/ ./
RUN if [ -z "$VITE_GOOGLE_CLIENT_ID" ]; then \
      echo "WARNING: VITE_GOOGLE_CLIENT_ID is empty; Google OAuth on frontend may not work."; \
    fi
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS backend-restore
WORKDIR /src/backend
COPY backend/MyGarage.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore MyGarage.csproj

FROM backend-restore AS backend-publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src/backend
COPY backend/ ./
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish MyGarage.csproj -c ${BUILD_CONFIGURATION} -o /app/publish --no-restore

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

