# The Artefacts app as one image: the API, serving the built frontend from wwwroot on port 8080.
# Build from the repository root:  docker build -t artefacts .
# Install and configuration: docker/truenas/README.md.

# --- Frontend ----------------------------------------------------------------------------------------------------
FROM node:24-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
# The typecheck covers tests, which read the system page definitions from the backend.
COPY backend/src/Artefacts.Infrastructure/Pages/Definitions/ /src/backend/src/Artefacts.Infrastructure/Pages/Definitions/
RUN npm run build

# --- API ---------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend
# Restore first, from the project files alone, so code changes reuse the package layer.
COPY backend/global.json backend/Directory.Build.props ./
COPY backend/src/Artefacts.Domain/Artefacts.Domain.csproj src/Artefacts.Domain/
COPY backend/src/Artefacts.Application/Artefacts.Application.csproj src/Artefacts.Application/
COPY backend/src/Artefacts.Infrastructure/Artefacts.Infrastructure.csproj src/Artefacts.Infrastructure/
COPY backend/src/Artefacts.Api/Artefacts.Api.csproj src/Artefacts.Api/
RUN dotnet restore src/Artefacts.Api/Artefacts.Api.csproj
COPY backend/src/ src/
# The OpenAPI document and GraphQL schema are written at build time for the frontend's generated client; the
# image does not need them (they are committed), so skip both.
RUN dotnet publish src/Artefacts.Api/Artefacts.Api.csproj --no-restore -c Release -o /app \
      -p:OpenApiGenerateDocuments=false -p:ExportGraphQLSchema=false

# --- Runtime -----------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Npgsql probes for GSSAPI on every connection and logs an error when the library is missing.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api /app ./
COPY --from=frontend /src/frontend/dist ./wwwroot/
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Database__MigrateOnStartup=true \
    Storage__Provider=FileSystem \
    Storage__FileSystem__Root=/data/media \
    Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Warning
# The image's non-root user (UID 1654). The media directory must be writable by it; see the install guide.
USER $APP_UID
EXPOSE 8080
VOLUME /data/media
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD ["dotnet", "/app/Artefacts.Api.dll", "--health-check"]
ENTRYPOINT ["dotnet", "/app/Artefacts.Api.dll"]
