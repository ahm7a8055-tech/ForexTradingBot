# ForexSignalBot：AI驱动的Telegram外汇信号 / 智能免费开源的Telegram机器人自动转发器 📈🤖✨🚀

[![License](https://img.shields.io/github/license/Opselon/ForexTradingBot?style=for-the-badge&color=blue)](https://github.com/Opselon/ForexTradingBot/blob/main/LICENSE "项目许可证徽章：表示代码库采用MIT许可证，允许开放使用和修改。点击查看许可证详情和使用条款。")
[![GitHub Stars](https://img.shields.io/github/stars/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/stargazers "GitHub星标数：显示为此仓库点赞的用户数量，反映了项目的受欢迎程度和关注度。点击查看点赞者列表。")
[![GitHub Forks](https://img.shields.io/github/forks/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/network/members "GitHub Fork数：显示此仓库被复刻的次数，表明了协作潜力和社区参与度。点击查看仓库的复刻列表。")
[![GitHub Issues](https://img.shields.io/github/issues/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/issues "GitHub开放问题数：显示当前未解决的问题数量，表明项目正在积极开发、跟踪错误并持续解决问题。点击查看开放的问题。")
[![GitHub Closed Issues](https://img.shields.io/github/issues-closed/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot/issues?q=is%3Aissue+is%3Aclosed "GitHub已关闭问题数：突显了项目在处理和解决已报告问题方面的响应能力。点击查看已关闭的问题。")
[![GitHub Pull Requests](https://img.shields.io/github/issues-pr/Opselon/ForexTradingBot?style=for-the-badge)](https://github.com/Opselon/ForexTradingBot/pulls "GitHub开放拉取请求数：显示正在审查中的活跃贡献和功能。点击查看开放的拉取请求。")
[![GitHub Closed Pull Requests](https://img.shields.io/github/issues-pr-closed/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot/pulls?q=is%3Apr+is%3Aclosed "GitHub已关闭拉取请求数：展示了社区贡献的成功整合。")
[![Test Coverage](https://img.shields.io/codecov/c/github/Opselon/ForexTradingBot/main?style=for-the-badge&logo=codecov)](https://codecov.io/gh/Opselon/ForexTradingBot "代码覆盖率：表示自动化测试覆盖的代码百分比，反映了代码的质量和可靠性。（注意：需要集成Codecov）")
[![Top Language](https://img.shields.io/github/languages/top/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot "主要编程语言：清楚地显示C#是项目中使用的主要语言，通常表明了核心技术栈和开发环境。")
[![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0 "目标.NET版本：指定项目所基于的.NET框架版本，突显其现代技术基础。")
[![Last Commit](https://img.shields.io/github/last-commit/Opselon/ForexTradingBot?style=for-the-badge&color=success)](https://github.com/Opselon/ForexTradingBot/commits/main "最后提交日期：显示代码库最近一次更新的时间，提供了项目活动和持续维护的迹象。点击查看提交历史。")
[![Commit Activity](https://img.shields.io/github/commit-activity/y/Opselon/ForexTradingBot?style=for-the-badge&label=Commits/年)](https://github.com/Opselon/ForexTradingBot/graphs/contributors "年度提交活动：显示过去一年中代码提交的频率，表明持续开发和积极维护。")
[![Code Size](https://img.shields.io/github/languages/code-size/Opselon/ForexTradingBot?style=for-the-badge&color=important)](https://github.com/Opselon/ForexTradingBot "总代码量：表示仓库中的总代码行数，提供了对项目规模和复杂性的粗略估计。点击查看代码量详情。")
[![Contributors](https://img.shields.io/github/contributors/Opselon/ForexTradingBot?style=for-the-badge)](https://github.com/Opselon/ForexTradingBot/graphs/contributors "贡献者数量：显示为该项目贡献代码的总人数，突显了社区参与和协作努力。")
[![GitHub Repo stars](https://img.shields.io/github/stars/Opselon/ForexTradingBot?style=social)](https://github.com/Opselon/ForexTradingBot/stargazers)
### 🚀 立即开始！

*   <span style="font-size: 3.5em;">**在线机器人：** [https://t.me/trade_ai_helper_bot](https://t.me/trade_ai_helper_bot) ✨</span>
*   *只需点击链接即可在Telegram中打开机器人并开始交易！*

![ForexSignalBot 演示](https://raw.githubusercontent.com/Opselon/ForexTradingBot/master/assets/lcak2Rr.gif)
---

## 🚀 开始使用

您可以通过两种方式运行此项目：使用Docker（推荐用于快速设置）或手动设置本地环境。

### 选项 1：使用Docker快速入门（推荐）

使用Docker在几分钟内运行整个应用程序堆栈——API、PostgreSQL数据库和Redis缓存。**这是开始的最快、最简单的方法。**

#### 先决条件

*   **Docker Desktop**：确保它已在您的系统上安装并运行。 [在此处下载](https://www.docker.com/products/docker-desktop/)。

#### 步骤 1：克隆仓库

打开您的终端并克隆项目源代码。
```bash
git clone https://github.com/Opselon/ForexTradingBot.git
cd ForexTradingBot
```

#### 步骤 2：配置您的密钥

应用程序需要API密钥和密码。我们为此使用一个`.env`文件，该文件会保持私密。

1.  **创建环境文件：**
    ```bash
    cp .env.example .env
    ```

2.  **编辑`.env`文件：** 打开新的`.env`文件并填写您的实际密钥值。
    *   `TELEGRAM_BOT_TOKEN`：从Telegram上的`@BotFather`获取。
    *   `POSTGRES_PASSWORD`：为您的数据库创建一个强大、安全的密码。

#### 步骤 3：运行应用程序！🔥

在Docker运行的情况下，从项目的根目录执行单个命令：
```bash
docker-compose up --build -d
```
此命令会构建并启动API、PostgreSQL和Redis容器。API被配置为**在启动时自动应用数据库迁移**。

#### 步骤 4：填充数据库
机器人需要一个初始的RSS源列表。使用像DBeaver或DataGrip这样的客户端连接到数据库，并运行`Populate_RssSources_Categories.sql`脚本。
*   **主机：** `localhost`
*   **端口：** `5432`
*   **数据库：** `forexsignalbot_db`
*   **用户：** `postgres`
*   **密码：** 您在`.env`中设置的`POSTGRES_PASSWORD`。

**🎉 就是这样！您的机器人现在正在Docker中运行。**

---

### 选项 2：本地开发环境设置（不使用Docker）

如果您希望直接在您的机器上运行应用程序，请按照以下步骤操作。

#### 先决条件

1.  **.NET 9 SDK：**
    *   安装 **.NET 9 SDK (v9.0.107 或更高版本)**。
    *   **下载页面：** [https://dotnet.microsoft.com/en-us/download/dotnet/9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
    *   通过运行 `dotnet --version` 来验证您的安装。

2.  **PostgreSQL 数据库：**
    *   安装并运行一个本地PostgreSQL服务器。
    *   创建一个数据库和一个用户。
    *   在`appsettings.Development.json`文件中更新您的连接字符串。

3.  **Redis 服务器：**
    *   Redis用于缓存和后台作业处理。
    *   **对于 Windows：** 安装一个与Redis兼容的服务器，如**Memurai**。
        *   **安装指南：** [https://docs.memurai.com/en/installation.html](https://docs.memurai.com/en/installation.html)
    *   **对于 macOS/Linux：** 通过包管理器安装（例如 `brew install redis` 或 `sudo apt-get install redis-server`）。

#### 在本地运行应用程序

对于希望直接在自己的机器上运行应用程序的开发人员，请按照以下步骤操作：

1.  **克隆仓库**（如果您还没有这样做）。
2.  **配置`appsettings.Development.json`**，填入您的本地数据库连接字符串和其他设置。
3.  **应用数据库迁移：**
    ```bash
    dotnet ef database update --startup-project WebApi --project Infrastructure
    ```
4.  通过在您的本地数据库上运行`Populate_RssSources_Categories.sql`脚本来**填充数据库**。
5.  **运行API：**
    ```bash
    dotnet run --project WebApi
    ```

有关更全面的详细信息和生产部署说明，请参阅专门的[INSTALL.md指南](https://github.com/Opselon/ForexTradingBot/blob/master/WebAPI/INSTALL.md)。

---

## 🛠️ 开发者指南

本节包含用于开发的常用命令。

### 管理数据库迁移

在运行这些命令之前，请确保您已安装EF Core工具：`dotnet tool install --global dotnet-ef`

*   **添加新的迁移：** 当您更改领域模型时，创建一个新的迁移。
    ```bash
    dotnet ef migrations add YourMigrationName --startup-project WebApi --project Infrastructure
    ```
    *（将`YourMigrationName`替换为一个描述性的名称，例如`AddSignalStatus`）*

*   **应用迁移：** 手动更新数据库架构。
    ```bash
    dotnet ef database update --startup-project WebApi --project Infrastructure
    ```

### 创建生产版本

要将应用程序编译为用于部署的自包含可执行文件：

```bash
# 一个自包含的 Windows x64 版本的示例
dotnet publish --configuration Release --runtime win-x64 --self-contained true --project WebApi
```
*   输出将位于`WebApi/bin/Release/net9.0/win-x64/publish`文件夹中。

---

### 步骤 4：Web设置向导 ✨
这是新的、简化的设置过程。

1.  **打开Web面板：** 在浏览器中导航到 **[http://localhost:5000/login.html](http://localhost:5000/login.html)**。
2.  **登录：** 使用默认凭据：
    *   **用户名：** `admin`
    *   **密码：** `admin`
    *（为了更好的安全性，在Web用户界面的首次设置过程中，系统会提示您更改这些敏感信息。）*
3.  **引导设置：** 首次登录后，您将自动重定向到一个安全的设置页面（`/indexapp.html`）。
    *   在此页面上，系统会提示您输入**Telegram机器人令牌**和其他核心设置。
    *   系统将在保存前**实时测试**您的凭据，以确保它们有效。
    *   一旦保存，这些设置将安全地存储在数据库中，而不是纯文本文件中。
4.  **数据库填充：** 初始设置后，系统会提示您填充数据库。点击Web用户界面中的“Seed Database”（填充数据库）按钮。这将填充初始的RSS源列表和其他所需数据。

**🎉 就是这样！您的机器人现已完全配置并正在运行。** 您可以从Web面板管理所有内容。
![alt text](https://raw.githubusercontent.com/Opselon/ForexTradingBot/master/assets/Dashboard-Dark.png.jpg)


## 🌟 Stargazers 随时间变化
如果这个项目对您有帮助，不妨给它一个 🌟
[![Stargazers over time](https://starchart.cc/Opselon/ForexTradingBot.svg?variant=light)](https://starchart.cc/Opselon/ForexTradingBot)

---

## 🌍 多语言README文件 🌍

我们提供多种语言的README文件，以使我们的项目能够为全球用户所用。请在下方选择您偏好的语言：

| 语言 | 语言代码 | README 文件 | 状态 |
|----------|---------------|-------------|---------|
| 英语 | 🇺🇸 EN | [README.md](README.md) | ✅ 完成 |
| 俄语 | 🇷🇺 RU | [README_RU.md](README_RU.md) | ✅ 完成 |
| 波斯语 | 🇮🇷 FA | [README_FA.md](README_FA.md) | ✅ 完成 |
| 中文 | 🇨🇳 ZH | [README_ZH.md](README_ZH.md) | ✅ 完成 |
| 西班牙语 | 🇪🇸 ES | [README_ES.md](README_ES.md) | ✅ 完成 |
| 法语 | 🇫🇷 FR | [README_FR.md](README_FR.md) | ✅ 完成 |
| 德语 | 🇩🇪 DE | [README_DE.md](README_DE.md) | ✅ 完成 |
| 土耳其语 | 🇹🇷 TR | [README_TR.md](README_TR.md) | ✅ 完成 |
| 阿拉伯语 | 🇸🇦 AR | [README_AR.md](README_AR.md) | ✅ 完成 |
| 印地语 | 🇮🇳 HI | [README_HI.md](README_HI.md) | ✅ 完成 |
| 意大利语 | 🇮🇹 IT | [README_IT.md](README_IT.md) | ✅ 完成 |
| 葡萄牙语 | 🇵🇹 PT | [README_PT.md](README_PT.md) | ✅ 完成 |

每个README文件都包含完整的项目文档、设置说明和功能，并已翻译成相应语言。所有文件都与最新的项目信息保持同步更新。
