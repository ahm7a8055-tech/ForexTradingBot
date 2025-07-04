# ForexSignalBot - Installation & Configuration Guide

This document provides comprehensive instructions for deploying the **ForexSignalBot** to a production server. It covers prerequisites, deployment methods, configuration, and a deep dive into the core data models for advanced features like the auto-forwarder and RSS aggregation.

**Table of Contents**
1.  [Prerequisites](#1-prerequisites)
2.  [Docker Deployment (Recommended)](#2-docker-deployment-recommended-)
3.  [Manual (Bare-Metal) Deployment](#3-manual-bare-metal-deployment-)
4.  [Configuration (`appsettings.production.json`) Explained](#4-configuration-appsettingsproductionjson-explained)
5.  [Core Features & Data Model Deep Dive](#5-core-features--data-model-deep-dive)
    *   [Advanced Auto-Forwarding Engine (`ForwardingRules`)](#advanced-auto-forwarding-engine-forwardingrules)
    *   [RSS Feed Aggregation & Categorization (`RssSources`, `SignalCategories`)](#rss-feed-aggregation--categorization-rsssources-signalcategories)

---

## 1. Prerequisites

Before you begin, ensure your server has the following software installed:

*   **Git:** To clone the repository.
*   **For Docker Deployment:**
    *   **Docker Engine:** [Install Docker Engine](https://docs.docker.com/engine/install/)
    *   **Docker Compose:** [Install Docker Compose](https://docs.docker.com/compose/install/)
*   **For Manual Deployment:**
    *   **.NET 9 SDK (or later):** [Download .NET 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
    *   **PostgreSQL Server:** A running instance of PostgreSQL with the `pg_trgm` extension enabled (`CREATE EXTENSION pg_trgm;`).
    *   **Redis Server:** A running instance of Redis.

---

## 2. Docker Deployment (Recommended) 🐳

This method uses Docker Compose to orchestrate the application, database, and cache containers.

### Step 1: Clone the Repository
```bash
git clone https://github.com/Opselon/ForexTradingBot.git
cd ForexTradingBot
```

### Step 2: Create the `.env` Configuration File
Create a file named `.env` in the project root. This file will store all your secrets. Copy the contents from `.env.example` as a template and fill in your production values.

### Step 3: Run the Application
Start the application in detached mode. This will build the images and start all services.
```bash
docker-compose up --build -d
```
The application, database, and cache are now running.

### Step 4: Seed the Database
The bot requires an initial set of data (RSS feeds, etc.). Execute the seeding script inside the running PostgreSQL container.

1.  Find the container ID: `docker-compose ps`
2.  Execute the script:
    ```bash
    docker exec -i <your_postgres_container_id> psql -U $POSTGRES_USER -d $POSTGRES_DB < Populate_RssSources_Categories.sql
    ```

Your bot is now fully deployed and operational!

---

## 3. Manual (Bare-Metal) Deployment 🔧
*(Instructions for manual deployment can be found in the project's README or previous documentation versions.)*

---

## 4. Configuration (`appsettings.production.json`) Explained
*(Instructions for `appsettings.production.json` can be found in the project's README or previous documentation versions.)*

---

## 5. Core Features & Data Model Deep Dive

This section provides a detailed look at the data models powering the bot's most powerful features. All examples are SQL for PostgreSQL and should be run against your database.

### Advanced Auto-Forwarding Engine (`ForwardingRules`)

The ForexSignalBot includes a highly sophisticated and customizable auto-forwarding engine, making it a powerful tool for automating Telegram message flows. This system allows you to automatically copy messages from source channels to one or more target channels with fine-grained control over message editing and filtering. This feature is ideal for signal providers, news aggregators, and community managers who need to streamline content distribution. All rules are configured directly in the `ForwardingRules` database table.

#### **Important Note on Telegram Channel IDs**
Telegram's API identifies channels and supergroups with a negative number that almost always starts with `-100`. For example, a channel ID might be `-1001234567890`. **You must store these IDs in the `ForwardingRules` table exactly as you get them from Telegram, including the negative sign.** The application logic in `UserApiForwardingOrchestrator.cs` uses these raw, negative IDs to match incoming messages to the correct rule.

#### `ForwardingRules` Table Breakdown:

*   **Rule Identification & Status:**
    *   `RuleName` (Primary Key): A unique, human-readable name for the rule (e.g., `Forward_Signals_To_Public_Channel`).
    *   `IsEnabled`: A simple boolean (`true`/`false`) to enable or disable the rule without deleting it.
    *   `SourceChannelId`: The numeric ID of the channel to monitor for messages (e.g., `-1001234567890`).
    *   `TargetChannelIds`: A JSONB array of channel IDs where messages should be forwarded. Example: `[-1009876543210, -1001122334455]`

*   **Message Editing Options (`EditOptions_` prefix):** These columns allow you to transform the message content before it's sent to the target channels.
    *   `PrependText` / `AppendText`: Add custom text to the beginning or end of the original message.
    *   `CustomFooter`: A dedicated field to add a standardized footer (e.g., a signature or advertisement). This is often used with `AppendText`.
    *   `RemoveSourceForwardHeader`: If `true`, this removes the "Forwarded from..." header that Telegram adds by default, making the message appear native to the target channel.
    *   `RemoveLinks`: If `true`, strips all hyperlinks (`http://`, `https://`, `t.me/`) from the message text.
    *   `StripFormatting`: If `true`, removes all Markdown/HTML formatting (bold, italics, etc.), sending only plain text.
    *   `DropAuthor`: If `true`, and the original message has an author signature, it will be removed.
    *   `DropMediaCaptions`: If `true`, forwards media (photos/videos) without their original captions. The `AppendText` or `CustomFooter` can still be applied as a new caption.
    *   `NoForwards`: If `true`, the rule will ignore any message that is itself a forward from another channel.

*   **Message Filtering Options (`FilterOptions_` prefix):** These columns give you precise control over *which* messages get forwarded.
    *   `AllowedMessageTypes`: A JSONB array of allowed message types. The `UserApiForwardingOrchestrator.cs` logic processes `text`, `photo`, and `document` types. Example: `["text", "photo"]`.
    *   `AllowedMimeTypes`: A JSONB array of allowed MIME types for documents. Only relevant if `document` is in `AllowedMessageTypes`. Example: `["application/pdf", "image/gif"]`.
    *   `ContainsText`: A text filter. The message will only be forwarded if its content includes this specific text.
    *   `ContainsTextIsRegex`: If `true`, treats `ContainsText` as a PostgreSQL regular expression for powerful pattern matching (e.g., `(BUY|SELL)` to match either word).
    *   `AllowedSenderUserIds` / `BlockedSenderUserIds`: JSONB arrays of user IDs to create whitelists or blacklists. Only messages sent by an allowed sender (and not by a blocked sender) will be forwarded.
    *   `IgnoreEditedMessages`: If `true`, the rule will not trigger on message edits. The `UserApiForwardingOrchestrator` is configured to process edits, so setting this to `true` will block them.
    *   `IgnoreServiceMessages`: If `true`, ignores channel service messages (e.g., "User joined the group", "Channel photo updated").
    *   `MinMessageLength` / `MaxMessageLength`: Filters messages based on their character count.

#### Example: Creating a Forwarding Rule

This example creates a rule to forward messages from a private "Signals VIP" channel to a public channel. It cleans the message by removing the author and header, adds a custom footer, and only forwards text messages containing the word "BUY" or "SELL" from a specific admin user.

```sql
INSERT INTO public."ForwardingRules" (
    "RuleName", "IsEnabled", "SourceChannelId", "TargetChannelIds",
    -- Edit Options
    "EditOptions_PrependText", "EditOptions_AppendText", "EditOptions_CustomFooter",
    "EditOptions_RemoveSourceForwardHeader", "EditOptions_RemoveLinks", "EditOptions_StripFormatting",
    "EditOptions_DropAuthor", "EditOptions_DropMediaCaptions", "EditOptions_NoForwards",
    -- Filter Options
    "FilterOptions_AllowedMessageTypes", "FilterOptions_AllowedMimeTypes",
    "FilterOptions_ContainsText", "FilterOptions_ContainsTextIsRegex", "FilterOptions_ContainsTextRegexOptions",
    "FilterOptions_AllowedSenderUserIds", "FilterOptions_BlockedSenderUserIds",
    "FilterOptions_IgnoreEditedMessages", "FilterOptions_IgnoreServiceMessages",
    "FilterOptions_MinMessageLength", "FilterOptions_MaxMessageLength"
)
VALUES
(
    'ForwardVipSignalsToPublic', -- A unique name for our rule
    true,                        -- This rule is active
    -1001234567890,              -- The private source channel ID (must be negative)
    '[-1009876543210]',          -- The public target channel ID (in a JSON array, also negative)
    -- Edit Options
    NULL,                               -- No text to prepend
    NULL,                               -- No text to append
    'Signal provided by MyBrand ✨ | Join VIP for more!', -- Add a clean footer
    true,                        -- Remove the 'Forwarded from...' header
    false,                       -- Keep links (e.g., to charts)
    false,                       -- Keep formatting (bold, etc.)
    true,                        -- Remove the original author's name
    false,                       -- Keep captions on photos/videos
    true,                        -- Do not forward messages that are already forwards
    -- Filter Options
    '["text"]',                  -- Only forward text messages
    '[]',                        -- No specific document types to filter
    '(BUY|SELL)',                -- The message must contain "BUY" or "SELL" (case-insensitive)
    true,                        -- Treat the filter text as a Regex
    1,                           -- Regex options (1 = IgnoreCase, see PostgreSQL docs for more)
    '[123456789]',               -- Only forward if the sender is this specific user ID
    '[]',                        -- No blocked users
    true,                        -- Ignore when messages are edited
    true,                        -- Ignore "user joined" type messages
    10,                          -- Minimum message length of 10 characters
    NULL                         -- No maximum length
);
```

### RSS Feed Aggregation & Categorization (`RssSources`, `SignalCategories`)

The bot's ability to provide real-time news comes from its powerful RSS aggregation engine, defined by the `RssReaderService.cs`. This system is configured via two core database tables: `SignalCategories` to organize types of news, and `RssSources` to store the actual feed URLs.

#### `SignalCategories` Table
This table acts as a simple container for the types of news and signals you want to manage. The `RssReaderService` uses this to classify incoming news items.

*   `Id`: A unique UUID for the category.
*   `Name`: The category name (e.g., "Forex News", "Crypto Updates", "Stock Market Analysis").
*   `IsActive`: Whether the category is currently in use.
*   `SortOrder`: An integer to control the display order in potential future UI applications.

#### `RssSources` Table
This table holds the individual RSS feeds the bot will poll for new articles. The `RssReaderService` iterates through all `IsActive` sources.

*   `Id`: A unique UUID for the source.
*   `Url`: The full URL of the RSS/Atom feed.
*   `SourceName`: A human-readable name for the source (e.g., "Investing.com Forex News").
*   `IsActive`: If `false`, the `RssReaderService` will ignore this source. The service can automatically set this to `false` if a source fails too many times.
*   `DefaultSignalCategoryId`: A foreign key linking this source to a default category in `SignalCategories`. This automatically classifies all news from this source.

#### Example: Adding RSS Feeds and Categories
This SQL script first creates two news categories and then adds two RSS feed sources, linking each to its appropriate category.

**Step 1: Create the Categories**
```sql
-- Generate UUIDs for your categories. You can use an online tool or pgcrypto's gen_random_uuid()
INSERT INTO public."SignalCategories" ("Id", "Name", "Description", "IsActive", "SortOrder")
VALUES
('a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1', 'Forex & Economic News', 'Major currency news and global economic updates.', true, 10),
('b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2', 'Cryptocurrency Analysis', 'News and analysis on major cryptocurrencies.', true, 20);
```

**Step 2: Add RSS Sources Linked to Those Categories**
```sql
-- Use the UUIDs generated in Step 1 for "DefaultSignalCategoryId"
INSERT INTO public."RssSources" ("Id", "Url", "SourceName", "IsActive", "DefaultSignalCategoryId")
VALUES
(
    -- Generate a new UUID for this source
    'c3c3c3c3-c3c3-c3c3-c3c3-c3c3c3c3c3c3',
    'https://www.dailyfx.com/feeds/market-news',
    'DailyFX Market News',
    true,
    'a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1' -- Links to 'Forex & Economic News'
),
(
    -- Generate another new UUID
    'd4d4d4d4-d4d4-d4d4-d4d4-d4d4d4d4d4d4',
    'https://cointelegraph.com/rss',
    'Cointelegraph News',
    true,
    'b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2' -- Links to 'Cryptocurrency Analysis'
);
```