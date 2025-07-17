# ForexSignalBot: ИИ-управляемые сигналы Forex для Telegram / Автоматический пересыльщик Telegram бота Умный Бесплатный Открытый исходный код 📈🤖✨🚀

[![License](https://img.shields.io/github/license/Opselon/ForexTradingBot?style=for-the-badge&color=blue)](https://github.com/Opselon/ForexTradingBot/blob/main/LICENSE "Project License Badge: Indicates the MIT License, allowing open use and modification of the codebase. Click to view license details and usage terms.)
[![GitHub Stars](https://img.shields.io/github/stars/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/stargazers "GitHub Stars Count: Shows how many users have starred this repository, reflecting popularity and interest in the project. Click to see stargazers.)
[![GitHub Forks](https://img.shields.io/github/forks/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/network/members "GitHub Forks Count: Displays the number of times this repository has been forked, indicating collaborative potential and community engagement. Click to view forks of the repository.")
[![GitHub Issues](https://img.shields.io/github/issues/Opselon/ForexTradingBot?style=for-the-badge&logo=github)](https://github.com/Opselon/ForexTradingBot/issues "GitHub Open Issues Count: Shows the number of currently open issues, indicating active development, bug tracking, and ongoing problem-solving efforts. Click to view open issues.")
[![GitHub Closed Issues](https://img.shields.io/github/issues-closed/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot/issues?q=is%3Aissue+is%3Aclosed "GitHub Closed Issues Count: Highlights the project's responsiveness in addressing and resolving reported issues. Click to view closed issues.")
[![GitHub Pull Requests](https://img.shields.io/github/issues-pr/Opselon/ForexTradingBot?style=for-the-badge)](https://github.com/Opselon/ForexTradingBot/pullsGitHubOpen Pull Requests Count: Shows active contributions and features in review. Click to view open pull requests.")
[![GitHub Closed Pull Requests](https://img.shields.io/github/issues-pr-closed/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot/pulls?q=is%3r+is%3Aclosed "GitHub Closed Pull Requests Count: Demonstrates successful integration of community contributions.")
[![Test Coverage](https://img.shields.io/codecov/c/github/Opselon/ForexTradingBot/main?style=for-the-badge&logo=codecov)](https://codecov.io/gh/Opselon/ForexTradingBot "Code Coverage: Indicates the percentage of code covered by automated tests, reflecting code quality and reliability. (Note: Requires Codecov integration))
[![Top Language](https://img.shields.io/github/languages/top/Opselon/ForexTradingBot?style=for-the-badge&color=informational)](https://github.com/Opselon/ForexTradingBot "Top Programming Language: Clearly displays C# as the primary language used in the project, often indicating the core technology stack and development environment.)
[![.NET Version](https://img.shields.io/badge/.NET-902D4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0 "Target .NET Version: Specifies the .NET framework version the project is built upon, highlighting its modern technological foundation.")
[![Last Commit](https://img.shields.io/github/last-commit/Opselon/ForexTradingBot?style=for-the-badge&color=success)](https://github.com/Opselon/ForexTradingBot/commits/main "Date of Last Commit: Shows how recently the codebase was updated, providing an indication of project activity and ongoing maintenance. Click to view commit history.")
[![Commit Activity](https://img.shields.io/github/commit-activity/y/Opselon/ForexTradingBot?style=for-the-badge&label=Commits/Year)](https://github.com/Opselon/ForexTradingBot/graphs/contributors "Yearly Commit Activity: Displays the frequency of code commits over the last year, indicating continuous development and active maintenance.")
[![Code Size](https://img.shields.io/github/languages/code-size/Opselon/ForexTradingBot?style=for-the-badge&color=important)](https://github.com/Opselon/ForexTradingBotTotal Code Size: Indicates the total lines of code in the repository, offering a rough estimate of the project's scale and complexity. Click for code size details.)
[![Contributors](https://img.shields.io/github/contributors/Opselon/ForexTradingBot?style=for-the-badge)](https://github.com/Opselon/ForexTradingBot/graphs/contributors "Number of Contributors: Shows the total number of individuals who have contributed code to this project, highlighting community involvement and collaborative efforts.)

### 🚀 Начните прямо сейчас!

*   <span style="font-size:30.5m;>**Живой бот:** [https://t.me/trade_ai_helper_bot](https://t.me/trade_ai_helper_bot) ✨</span>
*   *Просто нажмите на ссылку, чтобы открыть бота в Telegram и начать торговать!*

![ForexSignalBot Demo](https://raw.githubusercontent.com/Opselon/ForexTradingBot/master/assets/lcak2Rr.gif)

---

## 📋 Обзор проекта

**ForexSignalBot** - это передовая система на базе ИИ для Telegram, созданная для предоставления точных торговых сигналов в реальном времени для рынка Forex. Построена на **.NET 9** с использованием принципов чистой архитектуры и Domain-Driven Design.

### 🌟 Ключевые возможности

- **🤖 ИИ-генерация сигналов:** Продвинутые алгоритмы анализа рынка
- **📰 Агрегация новостей:** 100+ RSS-лент с умной категоризацией
- **💬 Полный UI в Telegram:** Интуитивный интерфейс прямо в мессенджере
- **🔗 Автоматическая пересылка:** Интеграция с торговыми платформами
- **🐳 Docker-контейнеризация:** Простое развертывание
- **🛡️ Безопасность:** Надежная обработка ошибок и защита данных

### 🏗️ Архитектура

- **Domain Layer:** Основная бизнес-логика
- **Application Layer:** Оркестрация use cases
- **Infrastructure Layer:** Внешние интеграции (PostgreSQL, Redis, Telegram API)
- **WebAPI:** REST API для веб-клиентов
- **TelegramPanel:** Обработка команд Telegram бота
- **BackgroundTasks:** Фоновые задачи (Hangfire)

### 🛠️ Технологии

- **.NET 9** - Основная платформа
- **PostgreSQL** - База данных
- **Redis** - Кэширование
- **Hangfire** - Фоновые задачи
- **Docker** - Контейнеризация
- **Entity Framework Core** - ORM
- **Polly** - Обработка ошибок

---

## 🚀 Быстрый старт

Вы можете запустить этот проект двумя способами: с Docker (рекомендуется для быстрой настройки) или настроив локальную среду вручную.

### Вариант 1рый старт с Docker (Рекомендуется)

Получите весь стек приложений — API, базу данных PostgreSQL и кэш Redis — работающими за минуты с Docker. **Это самый быстрый и простой способ начать.**

#### Предварительные требования

*   **Docker Desktop**: Убедитесь, что он установлен и запущен на вашей системе. [Скачайте здесь](https://www.docker.com/products/docker-desktop/).

#### Шаг 1: Клонирование репозитория

Откройте терминал и клонируйте исходный код проекта.
```bash
git clone https://github.com/Opselon/ForexTradingBot.git
cd ForexTradingBot
```

#### Шаг 2Настройка секретов

Приложению требуются API ключи и пароли. Мы используем файл `.env` для этого, который хранится в секрете.

1.  **Создайте файл окружения:**
    ```bash
    cp .env.example .env
    ```
2  **Отредактируйте файл `.env`:** Откройте новый файл `.env` и заполните ваши реальные секретные значения.
    *   `TELEGRAM_BOT_TOKEN`: Получите это от `@BotFather` в Telegram.
    *   `POSTGRES_PASSWORD`: Создайте надежный, безопасный пароль для вашей базы данных.

#### Шаг 3: Запуск приложения! 🔥

С запущенным Docker выполните одну команду из корневой директории проекта:
```bash
docker-compose up --build -d
```
Эта команда собирает и запускает контейнеры API, PostgreSQL и Redis. API настроен на **автоматическое применение миграций базы данных при запуске**.

#### Шаг 4лнение базы данных
Боту нужен начальный список RSS-лент. Подключитесь к базе данных, используя клиент типа DBeaver или DataGrip, и запустите скрипт `Populate_RssSources_Categories.sql`.
*   **Хост:** `localhost`
*   **Порт:** `5432`
*   **База данных:** `forexsignalbot_db`
*   **Пользователь:** `postgres`
*   **Пароль:** `POSTGRES_PASSWORD`, который вы установили в `.env`.

**🎉 Вот и все! Ваш бот теперь работает внутри Docker.**

---

### Вариант 2: Локальная настройка разработки (Без Docker)

Следуйте этим шагам, если вы предпочитаете запускать приложение напрямую на вашей машине.

#### Предварительные требования
1  **.NET 9**
    *   Установите **.NET 9 SDK (v9.0107 или новее)**.
    *   **Страница загрузки:** [https://dotnet.microsoft.com/en-us/download/dotnet/90//dotnet.microsoft.com/en-us/download/dotnet/90
    *   Проверьте вашу установку, запустив `dotnet --version`.
2 данных PostgreSQL:**
    *   Установите и запустите локальный сервер PostgreSQL.
    *   Создайте базу данных и пользователя.
    *   Обновите строку подключения в файле `appsettings.Development.json`.

3.  **Сервер Redis:**
    *   Redis используется для кэширования и обработки фоновых задач.
    *   **Для Windows:** Установите совместимый с Redis сервер типа **Memurai**.
        *   **Руководство по установке:** [https://docs.memurai.com/en/installation.html](https://docs.memurai.com/en/installation.html)
    *   **Для macOS/Linux:** Установите через менеджер пакетов (например, `brew install redis` или `sudo apt-get install redis-server`).

#### Запуск приложения локально

Для разработчиков, которые предпочитают запускать приложение напрямую на своей машине, следуйте этим шагам:1.  **Клонируйте репозиторий** (если еще не сделали).2  **Настройте `appsettings.Development.json`** с вашей локальной строкой подключения к базе данных и другими настройками.
3 **Примените миграции базы данных:**
    ```bash
    dotnet ef database update --startup-project WebApi --project Infrastructure
    ```
4  **Заполните базу данных**, запустив скрипт `Populate_RssSources_Categories.sql` против вашей локальной базы данных.
5.  **Запустите API:**
    ```bash
    dotnet run --project WebApi
    ```

Для более подробной информации и инструкций по развертыванию в продакшене, пожалуйста, обратитесь к специальному [руководству INSTALL.md](https://github.com/Opselon/ForexTradingBot/blob/master/WebAPI/INSTALL.md).

---

## 🛠️ Руководство разработчика

Этот раздел содержит общие команды для разработки.

### Управление миграциями базы данных

Перед запуском этих команд убедитесь, что у вас установлены инструменты EF Core: `dotnet tool install --global dotnet-ef`

*   **Добавить новую миграцию:** Когда вы изменяете доменную модель, создайте новую миграцию.
    ```bash
    dotnet ef migrations add YourMigrationName --startup-project WebApi --project Infrastructure
    ```
    *(Замените `YourMigrationName` описательным именем, например, `AddSignalStatus`)*

*   **Применить миграции:** Для ручного обновления схемы базы данных.
    ```bash
    dotnet ef database update --startup-project WebApi --project Infrastructure
    ```

### Создание продакшн сборки

Для компиляции приложения в автономный исполняемый файл для развертывания:

```bash
# Пример для автономной сборки Windows x64
dotnet publish --configuration Release --runtime win-x64elf-contained true --project WebApi
```
*   Результат будет в папке `WebApi/bin/Release/net90in-x64ublish`.

---

### Шаг 4: Веб-мастер настройки ✨
Это новый, упрощенный процесс настройки.

1.  **Откройте веб-панель:** Перейдите к **[http://localhost:5000ogin.html](http://localhost:500login.html)** в вашем браузере.2**Вход:** Используйте учетные данные по умолчанию:
    *   **Имя пользователя:** `admin`
    *   **Пароль:** `admin`
    *(Вам будет предложено изменить эти чувствительные детали во время процесса первоначальной настройки в веб-интерфейсе для лучшей безопасности.)*
3*Пошаговая настройка:** После первого входа вы будете автоматически перенаправлены на безопасную страницу настройки (`/indexapp.html`).
    *   На этой странице вам будет предложено ввести ваш **Токен Telegram бота** и другие основные настройки.
    *   Система будет **тестировать в реальном времени** ваши учетные данные, чтобы убедиться, что они действительны перед сохранением.
    *   После сохранения эти настройки хранятся безопасно в базе данных, а не в текстовых файлах.
4.  **Заполнение базы данных:** После первоначальной настройки вам будет предложено заполнить базу данных. Нажмите кнопкуSeed Database в веб-интерфейсе. Это заполнит начальный список RSS-лент и другие необходимые данные.

**🎉 Вот и все! Ваш бот теперь полностью настроен и работает.** Вы можете управлять всем из веб-панели.
![alt text](https://raw.githubusercontent.com/Opselon/ForexTradingBot/master/assets/Dashboard-Dark.png.jpg)

---

## 📊 Функции

- **📈 Торговые сигналы:** Точные buy/sell сигналы для всех основных валютных пар
- **📰 Новостная лента:** Агрегация из100источников с умной категоризацией
- **💳 Подписки:** Гибкая система членства с токен-кошельком
- **🔗 Автопересылка:** Прямая интеграция с торговыми платформами
- **⚙️ Настройки:** Персонализация уведомлений и категорий

---

## 🤝 Вклад в проект

1оркните репозиторий2Создайте ветку для новой функции
3. Внесите изменения4авьте Pull Request

---

## 📄 Лицензия

MIT License - свободное использование и модификация.

---

## 🌟 Звезды с течением времени
Если этот проект полезен для вас, вы можете поставить ему 🌟
[![Stargazers over time](https://starchart.cc/Opselon/ForexTradingBot.svg?variant=light)](https://starchart.cc/Opselon/ForexTradingBot)

---

#Теги: `#ForexTrading` `#TelegramBot` `#AISignals` `#NET9` `#OpenSource` 