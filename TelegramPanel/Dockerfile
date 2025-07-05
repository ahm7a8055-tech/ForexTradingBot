# Stage 1: Build the Application (Unified Stage)
# This approach mirrors the existing WebAPI Dockerfile for robustness.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy ALL files from your local project directory into the container.
COPY . .

# Run a fresh `dotnet restore` on the entire solution INSIDE the container.
# This sanitizes the build environment.
RUN dotnet restore "ForexTradingBot.sln"

# Now that the environment is clean, publish the TelegramPanel application.
RUN dotnet publish "TelegramPanel/TelegramPanel.csproj" -c Release -o /app/publish --no-restore


# Stage 2: Final Runtime Image for TelegramPanel
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for potential health checks (if it exposes an HTTP endpoint)
# TelegramPanel might expose a webhook.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN adduser --system --group --disabled-password --gecos "" --home /app appuser
USER appuser

# If TelegramPanel is an ASP.NET Core app listening on HTTP (e.g., for webhooks)
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Define a health check if TelegramPanel exposes an HTTP health endpoint (e.g., /healthz)
# This is a placeholder; the actual endpoint needs to be confirmed or implemented.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost/healthz || exit 1

ENTRYPOINT ["dotnet", "TelegramPanel.dll"]
