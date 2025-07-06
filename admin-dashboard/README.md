# Admin Dashboard for ForexTradingBot

This project is the admin dashboard for the ForexTradingBot application. It provides a user interface to manage various settings, configurations, and diagnostics for the backend services.

## Prerequisites

Before you begin, ensure you have the following installed:
- [Node.js](https://nodejs.org/) (LTS version recommended, e.g., v18, v20, v22)
- [npm](https://www.npmjs.com/) (comes with Node.js)

## Getting Started

### 1. Installation

Navigate to the `admin-dashboard` directory and install the dependencies:
```bash
cd admin-dashboard
npm install
```

### 2. Running in Development Mode

To start the development server with Hot Module Replacement (HMR):
```bash
npm run dev
```
The application will typically be available at `http://localhost:5173`.

### 3. Building for Production

To create a production build:
```bash
npm run build
```
The build artifacts will be placed in the `dist/` directory. These are the static files that can be served by a web server.

## Key Features

This admin dashboard allows you to:
- **Manage Telegram Bot Settings:** Configure bot token, admin user IDs, and logging chat ID.
- **Manage Telegram Client Settings:** Set up API ID and API Hash for the Telegram client library.
- **View System Diagnostics:** Check connectivity to the database and Telegram API.
- **Control Application (Conceptual):** Includes options for applying configurations and restarting the backend application (requires backend support).
- (Future features may include user management, RSS feed management, forwarding rule configuration, etc.)

## Connecting to the Backend

The admin dashboard communicates with the ForexTradingBot `WebAPI` project. Ensure the backend API is running and accessible. API calls from this dashboard are typically directed to endpoints under `/api/...` on the backend server.

You may need to configure the API base URL if the frontend is served from a different domain or port than the backend. This is often handled in Vite projects via proxy settings in `vite.config.js` during development, or by configuring the API service functions to use a full base URL for production builds.

## Available Scripts

In the project directory, you can run:

- `npm run dev`: Runs the app in development mode.
- `npm run build`: Builds the app for production.
- `npm run lint`: Lints the codebase using ESLint.
- `npm run preview`: Serves the production build locally for preview.
- `npm run test`: Runs tests using Jest.

## Project Structure

- `public/`: Static assets.
- `src/`: Main application source code.
  - `assets/`: Image and other static assets used by components.
  - `components/`: Reusable React components.
    - `auth/`: Authentication related components (e.g., `ProtectedRoute`).
    - `layout/`: Layout components (e.g., `AppLayout`, `Sidebar`, `Header`).
    - `ui/`: General UI components.
  - `contexts/`: React Context API providers (e.g., `AuthContext`, `ThemeContext`).
  - `pages/`: Top-level page components corresponding to routes.
  - `services/`: Modules for making API calls to the backend.
  - `App.jsx`: Main application component with routing setup.
  - `main.jsx`: Entry point of the React application.
  - `index.css`: Global styles and Tailwind CSS imports.
- `tailwind.config.js`: Tailwind CSS configuration.
- `vite.config.js`: Vite build tool configuration.
- `postcss.config.js`: PostCSS configuration.
- `jest.config.js`: Jest test runner configuration.
```
