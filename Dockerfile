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

# GradingService shells out to git to clone/fetch student repos; curl powers HEALTHCHECK
RUN apt-get update \
    && apt-get install -y --no-install-recommends git curl \
    && rm -rf /var/lib/apt/lists/*

# Persistent data lives under /app/data — mount this as a volume
RUN mkdir -p /app/data /app/backups /app/dp-keys \
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
