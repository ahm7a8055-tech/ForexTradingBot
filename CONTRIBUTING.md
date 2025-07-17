# Contributing to ForexSignalBot 🚀

First off, thank you for considering contributing to ForexSignalBot! It's people like you that make the open-source community such a powerful and innovative place. We are thrilled to welcome contributions from everyone, whether it's reporting a bug, suggesting a new feature, or writing code.

Every contribution is valuable, and we appreciate your willingness to help us build a smarter, more accessible trading ecosystem for everyone.

## 📜 Code of Conduct

We are committed to fostering an open, welcoming, and inclusive community. Before contributing, please take a moment to read our [Code of Conduct](CODE_OF_CONDUCT.md). We expect all contributors to adhere to its principles.

## 💡 How You Can Contribute

There are many ways to contribute to the project's success. Here are a few ideas:

*   **🐞 Reporting Bugs:** If you encounter a bug, please open an issue on our [GitHub Issues](https://github.com/Opselon/ForexTradingBot/issues) page. Use the "Bug Report" template and provide as much detail as possible.
*   **✨ Suggesting Enhancements:** Have an idea for a new feature or an improvement? Open an issue using the "Feature Request" template. We'd love to hear your thoughts on everything from new signal indicators to UI/UX enhancements.
*   **✍️ Improving Documentation:** If you notice areas where the documentation could be clearer or more detailed, don't hesitate to open an issue or a pull request.
*   **💻 Writing Code:** You can help by writing code to fix bugs or implement new features.

### Looking for a Place to Start?

Unsure where to begin? Check out these labels in our issue tracker:
*   [**good first issue**](https://github.com/Opselon/ForexTradingBot/labels/good%20first%2eissue) - Perfect for newcomers, these issues typically require only a few lines of code.
*   [**help wanted**](https://github.com/Opselon/ForexTradingBot/labels/help%20wanted) - These are well-defined tasks that are ready for a contributor to pick up.
*   [**feature**](https://github.com/Opselon/ForexTradingBot/labels/feature) - Help us build the future of the bot by working on a new feature from our [roadmap](#-roadmap--future-plans-the-path-to-2025-and-beyond-️).

## 🛠️ Development Setup: Ignite Your Bot 🔥

Our goal is to make setting up your development environment as quick and easy as possible. We follow a **dotnet ducker** 🐳 philosophy, with Docker being the recommended approach.

### Option 1: Quick Start with Docker (Recommended)

This is the fastest way to get the entire application stack—API, PostgreSQL database, and Redis cache—running in minutes.

1.  **Clone the repo:** `git clone https://github.com/Opselon/ForexTradingBot.git`
2.  **Configure Secrets:** Copy `.env.example` to `.env` and fill in your secrets (like the `TELEGRAM_BOT_TOKEN`).
3.  **Run!** Execute the `start.sh` (Linux/macOS) or `start.bat` (Windows) script.

For detailed instructions, please refer to the [Getting Started with Docker](https://github.com/Opselon/ForexTradingBot#getting-started-with-docker) section in the `README.md`.

### Option 2: Local Development Setup (Without Docker)

If you prefer a manual setup, you'll need to install the .NET 9 SDK, PostgreSQL, and Redis. Please follow the detailed guide in the [Local Development Setup](https://github.com/Opselon/ForexTradingBot#option-2-local-development-setup-without-docker) section of the `README.md`.

## ✅ Pull Request Process

1.  **Fork the Repository:** Create your own copy of the project to work on.
2.  **Create a Branch:** Create a descriptive branch from `master`. We suggest the following conventions:
    *   `feature/your-feature-name` (e.g., `feature/add-rsi-indicator`)
    *   `bugfix/issue-123` (e.g., `bugfix/fix-rss-deduplication`)
    *   `docs/update-readme`
3.  **Make Your Changes:** Write your code and make your changes.
4.  **Follow the Architecture:** Your contributions should respect the existing **Clean Architecture** and **Domain-Driven Design (DDD)** principles. Ensure your logic fits into the correct project layer (Domain, Application, Infrastructure, etc.).
5.  **Add Database Migrations:** If you change a domain model (an entity in the `Domain` project), you **must** create a new migration. Run this command from the root directory:
    ```sh
    dotnet ef migrations add YourMigrationName --startup-project WebApi --project Infrastructure
    ```
6.  **Add Tests:** We value quality and reliability. Please add unit tests for any new features or bug fixes. Test projects are located in the `Tests` folder.
7.  **Update Documentation:** If you've added or changed a feature, please update the `README.md` or other relevant documentation.
8.  **Open a Pull Request:** Push your branch to your fork and open a PR against the `master` branch of the main repository. Link the PR to any related issues and provide a clear description of your changes.

We will review your PR as soon as possible and work with you to get it merged. Thank you for helping us make ForexSignalBot better! 🌟
