# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AutoCheck.csproj .
RUN dotnet restore AutoCheck.csproj

COPY . .
RUN dotnet publish AutoCheck.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# GradingService shells out to git to clone/fetch student repos; curl powers HEALTHCHECK.
# Retry apt-get update — Ubuntu mirrors occasionally return a mid-sync/corrupt index.
RUN for i in 1 2 3; do apt-get update && break || sleep 5; done \
    && apt-get install -y --no-install-recommends git curl \
    && rm -rf /var/lib/apt/lists/*

# Persistent data lives under /app/data — mount these as volumes.
# /app/backup-repo is the local clone used to mirror backups into a private git repo.
RUN mkdir -p /app/data /app/backups /app/dp-keys /app/backup-repo \
    && groupadd -r autocheck && useradd -r -g autocheck -d /app autocheck \
    && chown -R autocheck:autocheck /app

COPY --from=build /app/publish .
RUN chown -R autocheck:autocheck /app

USER autocheck

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AutoCheck.dll"]
