# Stage 1: Build the Application (Unified Stage)
# This approach mirrors the existing WebAPI Dockerfile for robustness.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy ALL files from your local project directory into the container.
COPY . .

# Run a fresh `dotnet restore` on the entire solution INSIDE the container.
# This sanitizes the build environment.
RUN dotnet restore "ForexTradingBot.sln"

# Now that the environment is clean, publish the BackgroundTasks application.
RUN dotnet publish "BackgroundTasks/BackgroundTasks.csproj" -c Release -o /app/publish --no-restore


# Stage 2: Final Runtime Image for BackgroundTasks
# Using aspnet:9.0 for consistency with other services.
# For a worker service, dotnet/runtime:9.0 could be used for a smaller image if no ASP.NET Core features are needed.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# curl is not typically needed for a worker service unless it has a specific HTTP diagnostic endpoint.
# If procps is needed for a process-based healthcheck, it would be installed here.
# RUN apt-get update && apt-get install -y --no-install-recommends procps && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN adduser --system --group --disabled-password --gecos "" --home /app appuser
USER appuser

# Worker services do not typically listen on HTTP ports.
# ENV ASPNETCORE_URLS=http://+:80
# EXPOSE 80

# A health check for a worker service might involve checking if the process is running
# or if it's updating a status file/heartbeat.
# Example (requires procps with pgrep):
# HEALTHCHECK --interval=1m --timeout=30s --start-period=30s --retries=3 \
#   CMD pgrep -f 'dotnet BackgroundTasks.dll' > /dev/null || exit 1
# For now, no specific healthcheck is added. It can be implemented if needed.

ENTRYPOINT ["dotnet", "BackgroundTasks.dll"]
