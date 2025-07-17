# ForexSignalBot: 人工智能驱动的Telegram外汇信号机器人 / Telegram机器人自动转发器 智能免费开源 📈🤖✨🚀

[![License](https://img.shields.io/github/license/Opselon/ForexTradingBot?style=for-the-badge&color=blue)](https://github.com/Opselon/ForexTradingBot/blob/main/LICENSE "Project License Badge: Indicates the MIT License, allowing open use and modification of the codebase. Click to view license details and usage terms.")
[![GitHub Stars](https://img.shields.io/github/stars/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/stargazers "GitHub Stars Count: Shows how many users have starred this repository, reflecting popularity and interest in the project. Click to see stargazers.")
[![GitHub Forks](https://img.shields.io/github/forks/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/network/members "GitHub Forks Count: Displays the number of times this repository has been forked, indicating collaborative potential and community engagement. Click to view forks of the repository.")
[![GitHub Issues](https://img.shields.io/github/issues/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/issues "GitHub Open Issues Count: Shows the number of currently open issues, indicating active development, bug tracking, and ongoing problem-solving efforts. Click to view open issues.")
[![GitHub Closed Issues](https://img.shields.io/github/issues-closed/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot/issues?q=is%3Aissue+is%3Aclosed "GitHub Closed Issues Count: Highlights the project's responsiveness in addressing and resolving reported issues. Click to view closed issues.")
[![GitHub Pull Requests](https://img.shields.io/github/issues-pr/Opselon/ForexTradingBot?style=for-the-badge)](https://github.com/Opselon/ForexTradingBot/pulls "GitHub Open Pull Requests Count: Shows active contributions and features in review. Click to view open pull requests.")
[![GitHub Closed Pull Requests](https://img.shields.io/github/issues-pr-closed/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot/pulls?q=is%3Apr+is%3Aclosed "GitHub Closed Pull Requests Count: Demonstrates successful integration of community contributions.")
[![Test Coverage](https://img.shields.io/codecov/c/github/Opselon/ForexTradingBot/main?style=for-the-badge&logo=codecov)](https://codecov.io/gh/Opselon/ForexTradingBot "Code Coverage: Indicates the percentage of code covered by automated tests, reflecting code quality and reliability. (Note: Requires Codecov integration)")
[![Top Language](https://img.shields.io/github/languages/top/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot "Top Programming Language: Clearly displays C# as the primary language used in the project, often indicating the core technology stack and development environment.")
[![.NET Version](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0 "Target .NET Version: Specifies the .NET framework version the project is built upon, highlighting its modern technological foundation.")
[![Last Commit](https://img.shields.io/github/last-commit/Opselon/ForexTradingBot?style=for-the-badge&color=success)](https://github.com/Opselon/ForexTradingBot/commits/main "Date of Last Commit: Shows how recently the codebase was updated, providing an indication of project activity and ongoing maintenance. Click to view commit history.")
[![Commit Activity](https://img.shields.io/github/commit-activity/y/Opselon/ForexTradingBot?style=for-the-badge&label=Commits/Year)](https://github.com/Opselon/ForexTradingBot/graphs/contributors "Yearly Commit Activity: Displays the frequency of code commits over the last year, indicating continuous development and active maintenance.")
[![Code Size](https://img.shields.io/github/languages/code-size/Opselon/ForexTradingBot?style=for-the-badge&color=important)](https://github.com/Opselon/ForexTradingBot "Total Code Size: Indicates the total lines of code in the repository, offering a rough estimate of the project's scale and complexity. Click for code size details.")
[![Contributors](https://img.shields.io/github/contributors/Opselon/ForexTradingBot?style=for-the-badge)](https://github.com/Opselon/ForexTradingBot/graphs/contributors "Number of Contributors: Shows the total number of individuals who have contributed code to this project, highlighting community involvement and collaborative efforts.")


### 🚀 立即开始！

*   <span style=font-size:30.5>**实时机器人:** [https://t.me/trade_ai_helper_bot](https://t.me/trade_ai_helper_bot) ✨</span>
*   *只需点击链接即可在Telegram中打开机器人并开始交易！*

![ForexSignalBot Demo](https://raw.githubusercontent.com/Opselon/ForexTradingBot/master/assets/lcak2Rr.gif)

---

## 📋 项目概述

**ForexSignalBot** 是一个基于人工智能的先进Telegram系统，专为外汇市场提供精确的实时交易信号而设计。该项目基于 **.NET 9* 构建，采用清洁架构和领域驱动设计原则。

### 🌟 核心特性

- **🤖 AI信号生成:** 先进的市场分析算法
- **📰 新闻聚合:** 100分类
- **💬 Telegram完整UI:** 直观的即时通讯界面
- **🔗 自动转发:** 与交易平台集成
- **🐳 Docker容器化:** 简单部署
- **🛡️ 安全可靠:** 错误处理和数据保护

### 🏗️ 系统架构

项目基于**清洁架构**和**领域驱动设计**原则构建：

- **领域层:** 核心业务逻辑和实体
- **应用层:** 应用服务和用例编排
- **基础设施层:** 数据库和外部API实现
- **WebAPI:** REST API控制器
- **TelegramPanel:** Telegram机器人交互管理
- **BackgroundTasks:** 使用Hangfire的后台任务处理

### 🛠️ 技术栈

- **.NET9 - 主要开发平台
- **PostgreSQL** - 关系型数据库
- **Redis** - 缓存和后台处理
- **Hangfire** - 后台任务管理
- **Docker** - 容器化和部署
- **Entity Framework Core** - ORM框架
- **Polly** - 错误处理和弹性

---

## 🚀 快速开始

您可以通过两种方式运行此项目：使用Docker（推荐快速设置）或手动设置本地环境。

### 选项1：使用Docker快速开始（推荐）

在几分钟内使用Docker运行整个应用程序堆栈—API、PostgreSQL数据库和Redis缓存。**这是最快和最简单的开始方式。**

#### 先决条件

*   **Docker Desktop**: 确保它已安装并在您的系统上运行。 [在此下载](https://www.docker.com/products/docker-desktop/)。

#### 步骤1：克隆仓库

打开终端并克隆项目源代码。
```bash
git clone https://github.com/Opselon/ForexTradingBot.git
cd ForexTradingBot
```

#### 步骤2：配置您的密钥

应用程序需要API密钥和密码。我们使用`.env`文件，该文件保持私有。1.  **创建环境文件:**
    ```bash
    cp .env.example .env
    ```
2.  **编辑`.env`文件:** 打开新的`.env`文件并填写您的实际密钥值。
    *   `TELEGRAM_BOT_TOKEN`: 从Telegram的`@BotFather`获取。
    *   `POSTGRES_PASSWORD`: 为您的数据库创建强密码。

#### 步骤3：运行应用程序！🔥

在Docker运行的情况下，从项目根目录执行单个命令：
```bash
docker-compose up --build -d
```
此命令构建并启动API、PostgreSQL和Redis容器。API配置为**在启动时自动应用数据库迁移**。

#### 步骤4填充数据库
机器人需要RSS源的初始列表。使用DBeaver或DataGrip等客户端连接到数据库并运行`Populate_RssSources_Categories.sql`脚本。
*   **主机:** `localhost`
*   **端口:** `5432*   **数据库:** `forexsignalbot_db`
*   **用户:** `postgres`
*   **密码:** 您在`.env`中设置的`POSTGRES_PASSWORD`。

**🎉 就是这样！您的机器人现在在Docker中运行。**

---

### 选项2：本地开发设置（无Docker）

如果您更喜欢直接在机器上运行应用程序，请按照以下步骤操作。

#### 先决条件1  **.NET9 SDK:**
    *   安装**.NET9 SDK（v9.0107高版本）**。
    *   **下载页面:** [https://dotnet.microsoft.com/en-us/download/dotnet/90//dotnet.microsoft.com/en-us/download/dotnet/9.0   *   通过运行`dotnet --version`验证您的安装。
2.  **PostgreSQL数据库:**
    *   安装并运行本地PostgreSQL服务器。
    *   创建数据库和用户。
    *   在`appsettings.Development.json`文件中更新连接字符串。
3 **Redis服务器:**
    *   Redis用于缓存和后台作业处理。
    *   **Windows:** 安装Redis兼容服务器如**Memurai**。
        *   **安装指南:** [https://docs.memurai.com/en/installation.html](https://docs.memurai.com/en/installation.html)
    *   **macOS/Linux:** 通过包管理器安装（例如，`brew install redis`或`sudo apt-get install redis-server`）。

#### 本地运行应用程序

对于喜欢直接在机器上运行应用程序的开发人员，请按照以下步骤操作：

1 **克隆仓库**（如果还没有）。
2.  **配置`appsettings.Development.json`** 使用本地数据库连接字符串和其他设置。3  **应用数据库迁移:**
    ```bash
    dotnet ef database update --startup-project WebApi --project Infrastructure
    ```4.  **填充数据库** 通过对本地数据库运行`Populate_RssSources_Categories.sql`脚本。
5**运行API:**
    ```bash
    dotnet run --project WebApi
    ```

有关更详细的详细信息和生产部署说明，请参阅专门的[INSTALL.md指南](https://github.com/Opselon/ForexTradingBot/blob/master/WebAPI/INSTALL.md)。

---

## 🛠️ 开发者指南

本节包含开发的常用命令。

### 管理数据库迁移

在运行这些命令之前，确保已安装EF Core工具：`dotnet tool install --global dotnet-ef`

*   **添加新迁移:** 当您更改域模型时，创建新迁移。
    ```bash
    dotnet ef migrations add YourMigrationName --startup-project WebApi --project Infrastructure
    ```
    *（用描述性名称替换`YourMigrationName`，例如`AddSignalStatus`）*

*   **应用迁移:** 手动更新数据库架构。
    ```bash
    dotnet ef database update --startup-project WebApi --project Infrastructure
    ```

### 创建生产构建

将应用程序编译为用于部署的自包含可执行文件：

```bash
# Windows x64自包含构建示例
dotnet publish --configuration Release --runtime win-x64elf-contained true --project WebApi
```
*   输出将在`WebApi/bin/Release/net9064blish`文件夹中。

---

### 步骤4Web设置向导 ✨
这是新的、简化的设置过程。

1 **打开Web面板:** 在浏览器中导航到**[http://localhost:50ogin.html](http://localhost:5000in.html)**。
2  **登录:** 使用默认凭据：
    *   **用户名:** `admin`
    *   **密码:** `admin`
    *（您将在Web UI的首次设置过程中被提示更改这些敏感详细信息以提高安全性。）*
3.  **引导设置:** 首次登录后，您将自动重定向到安全设置页面（`/indexapp.html`）。
    *   在此页面上，您将被提示输入**Telegram机器人令牌**和其他核心设置。
    *   系统将**实时测试**您的凭据以确保它们在保存前有效。
    *   保存后，这些设置安全地存储在数据库中，而不是纯文本文件中。4.  **数据库填充:** 初始设置后，您将被提示填充数据库。在Web UI中点击Seed Database"按钮。这将填充RSS源的初始列表和其他必需数据。

**🎉 就是这样！您的机器人现在完全配置并运行。** 您可以从Web面板管理所有内容。
![alt text](https://raw.githubusercontent.com/Opselon/ForexTradingBot/master/assets/Dashboard-Dark.png.jpg)

---

## 📊 主要功能

### 📈 交易信号
- 为所有主要货币对提供精确的买入/卖出信号
- 基于AI算法的先进市场分析
- 支持USD、EUR、JPY、GBP、AUD、CAD、CHF、NZD货币对

### 📰 新闻和分析
- 从10+可信来源聚合新闻
- 智能新闻分类（外汇、股票、商品、加密货币）
- 自动去重功能
- 个性化用户设置

### 💳 会员系统
- 多层次会员资格（免费和付费）
- 基于令牌的钱包系统
- 交易和订阅管理

### 🔗 自动信号转发
- 与交易平台直接集成
- 减少手动错误和时间延迟
- 用户对风险设置的完全控制

---

## 🎨 用户界面

### Telegram机器人
- 完整交互式用户界面
- 内联键盘便于导航
- MarkdownV2式美化消息
- 交易确认交互按钮

### Web面板（开发中）
- 管理员仪表板
- 高级用户面板
- 分析图表和报告

---

## 🤝 贡献指南

我们欢迎开发者社区的贡献：1. Fork仓库
2. 创建功能分支34. 提交Pull Request

---

## 📄 许可证

本项目采用 **MIT许可证** 发布，允许自由使用、复制、修改和分发。

---

## 🌟 随时间变化的星标
如果这个项目对您有帮助，您可能希望给它一个 🌟
[![Stargazers over time](https://starchart.cc/Opselon/ForexTradingBot.svg?variant=light)](https://starchart.cc/Opselon/ForexTradingBot)

---

#标签: `#ForexTrading` `#TelegramBot` `#AISignals` `#NET9` `#OpenSource` 