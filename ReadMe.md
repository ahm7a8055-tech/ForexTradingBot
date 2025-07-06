# ForexTradingBot Project

Welcome to the ForexTradingBot project! This is a comprehensive system designed for managing and automating tasks related to forex trading signals, news aggregation, and Telegram bot interactions. It includes a backend API, a Telegram bot interface, an admin dashboard, and background task processing.

## Project Overview

The project is composed of several key components:

-   **`WebAPI/`**: The core backend of the application. It exposes a RESTful API for various functionalities, including managing settings, forwarding rules, RSS feeds, and user data. This is the central hub that other components interact with.
    -   See [WebAPI Installation and Configuration Guide](./WebAPI/INSTALL.md) for detailed setup instructions.
-   **`TelegramPanel/`**: This component is responsible for all direct interactions with the Telegram Bot API. It handles incoming messages, commands, callbacks, and orchestrates bot responses and actions based on the backend logic and configurations.
-   **`Application/`**: Contains the core application logic, including services, DTOs, interfaces, and features (CQRS handlers). This is a class library shared across `WebAPI`, `TelegramPanel`, etc.
-   **`Domain/`**: Defines the domain entities, enums, and value objects for the project.
-   **`Infrastructure/`**: Implements data access (repositories, database context), external service integrations (like Telegram API clients, payment gateways), caching, and other infrastructure concerns.
-   **`BackgroundTasks/`**: A separate process for handling long-running or scheduled background jobs, such as sending notifications, cleaning up user data, or processing queued tasks. It likely integrates with a job scheduling library like Hangfire.

## Getting Started

The recommended way to deploy and run the entire system is using Docker.

### Docker Deployment (Recommended)

The project includes a `docker-compose.yml` file and instructions for Docker-based deployment in the [WebAPI Installation Guide](./WebAPI/INSTALL.md). This method simplifies the setup of the WebAPI, database (PostgreSQL), cache (Redis), and potentially other services.

### Manual Setup

If you prefer a manual setup:

1.  **Backend (`WebAPI` and `TelegramPanel`):**
    *   Start by setting up the `WebAPI`. Follow the instructions in [WebAPI/INSTALL.md](./WebAPI/INSTALL.md). This includes database setup, configuration in `appsettings.json`, and running the API project.
    *   The `TelegramPanel` typically runs alongside or as part of the `WebAPI` deployment, or as a separate service that communicates with the `WebAPI`. Ensure its configurations are also set up according to any specific instructions (often part of the main backend setup).
2.  **Admin Dashboard:**
    *   The new Admin Dashboard will be served directly from `WebAPI/wwwroot` (accessible via `/index.html` or `/login.html` once implemented) and will not require a separate Node.js process.
    *   Ensure the admin dashboard can communicate with the `WebAPI` endpoints.
3.  **Background Tasks (`BackgroundTasks/`):**
    *   This component is typically run as a separate service. Instructions for its setup and execution would be similar to the `WebAPI` if it's a .NET executable.

## Configuration

-   **Backend Services (`WebAPI`, `TelegramPanel`, `BackgroundTasks`):** Configuration is primarily managed via `appsettings.json` files (and environment-specific versions like `appsettings.Production.json`) and environment variables. For dynamic settings like Telegram bot tokens, API keys, and operational parameters, the system uses a database-backed `SettingsService`, manageable via the new Admin Dashboard.
-   **Admin Dashboard:** The new Admin Dashboard is served from the same origin as the API, simplifying configuration.

## Contribution

Details on contributing to the project, coding standards, and pull request processes will be added here or in a separate `CONTRIBUTING.md` file.

---

This root `README.md` provides a high-level overview. For detailed information on specific components, please refer to their respective `README.md` or `INSTALL.md` files linked above.
```
