# Security Policy for ForexSignalBot 🛡️

## Introduction

The security of ForexSignalBot is a top priority for the Opselon team. We are deeply committed to building a secure, reliable, and trustworthy platform for all our users and developers. The integrity of your data and the stability of the bot are paramount.

We believe that a strong partnership with the global security research community is essential to achieving these goals. We value the work of independent researchers and appreciate your efforts in helping us keep our platform safe. If you have discovered a security vulnerability, we encourage you to report it to us privately and responsibly. We promise to engage with you in a respectful, timely, and collaborative manner.

## Scope

This security policy applies to vulnerabilities discovered within the ForexSignalBot project and its core infrastructure managed by the Opselon team.

#### ✅ In Scope

*   The source code in the main `Opselon/ForexTradingBot` GitHub repository, including all layers (`WebApi`, `TelegramPanel`, `Application`, `Domain`, `Infrastructure`).
*   The official containerized deployment configurations (`docker-compose.yml`, `Dockerfile`).
*   Vulnerabilities that can be demonstrated to have a direct security impact on a default installation of the software.

#### ❌ Out of Scope

*   **User-deployed instances:** The security of the server, network, or environment where you deploy the bot is your responsibility.
*   **Vulnerabilities in third-party dependencies:** Please report these directly to the respective project maintainers. However, if a dependency vulnerability affects ForexSignalBot, please let us know so we can update it.
*   **The official Telegram Bot (`@trade_ai_helper_bot`) itself:** While we strive to secure it, reports on the live bot should be made with extreme care to not affect other users. Theoretical vulnerabilities are preferred over exploits against the live system.
*   The security of Telegram's platform, PostgreSQL servers, or Redis servers themselves.

## Supported Versions

Our development is fast-paced to deliver continuous innovation. Therefore, security patches will only be applied to the most recent code on the `master` branch, which represents the next stable release. We do not provide security support for older versions. We strongly encourage all users to run the latest version of the software to benefit from the newest features and security fixes.

| Version             | Supported          |
| ------------------- | ------------------ |
| `master` branch     | :white_check_mark: |
| All previous versions | :x:                |

## Reporting a Vulnerability

We take all security reports with the highest seriousness and urgency. If you believe you have found a security vulnerability, please help us by disclosing it to us privately.

**⚠️ Please do not report security vulnerabilities through public GitHub issues, pull requests, or any public forum.**

Instead, please use one of the following private channels:

1.  **GitHub Private Vulnerability Reporting (Recommended):** Use the [**"Report a vulnerability"** feature](https://github.com/Opselon/ForexTradingBot/security/advisories/new) on GitHub. This is the most secure and efficient method, ensuring the report is delivered directly to the project maintainers.
2.  **Email:** If you prefer, you can send a detailed report to our security team at `security@opselon.com`. Please use the subject line: `Security Vulnerability Report: ForexSignalBot`.

### What to Include in Your Report

A detailed and clear report will help us resolve the issue faster. Please include:

*   **A clear and descriptive title** for the vulnerability.
*   **The component or location** where the vulnerability exists (e.g., `WebApi`, a specific controller, a function in the `Application` layer).
*   **A detailed description of the vulnerability** and its potential impact (e.g., Remote Code Execution, SQL Injection, Cross-Site Scripting).
*   **Step-by-step instructions** to reproduce the issue, including any necessary configurations, scripts, or code snippets.
*   **A Proof-of-Concept (PoC)** that demonstrates the vulnerability in a non-destructive way.
*   **Your contact information** and, if you wish, a link to your public profile for credit.

### Out-of-Scope Vulnerabilities (Common Exclusions)

The following types of findings are typically considered out of scope, unless they can be chained with other vulnerabilities to demonstrate a higher impact:

*   Findings related to SSL/TLS best practices or certificate configuration.
*   Missing security headers (e.g., `X-Frame-Options`, `Content-Security-Policy`) on non-sensitive pages.
*   Self-XSS (Cross-Site Scripting that requires the user to inject the payload themselves).
*   Denial of Service (DoS) attacks that require excessive resources.
*   Social engineering or phishing attacks against project members or users.
*   Reports on outdated software versions without a working PoC on the `master` branch.

## Our Security Disclosure Process

When you submit a security report, we follow a structured process to ensure every report is handled efficiently and fairly.

1.  **Phase 1: Triage (Within 2 Business Days)**
    *   We will send you an email acknowledging receipt of your report.
    *   We will validate the vulnerability and assign it a severity level, typically based on the CVSS (Common Vulnerability Scoring System).

2.  **Phase 2: Remediation (Timeline Varies)**
    *   Our team will conduct a deep-dive investigation and begin developing a patch.
    *   We will maintain an open line of communication, providing you with updates on our progress (e.g., every 5-7 business days). The timeline for a fix can vary from a few days to several weeks depending on the complexity of the vulnerability.

3.  **Phase 3: Coordinated Disclosure (After Patch is Ready)**
    *   Once the patch is ready and tested, we will coordinate a release date with you.
    *   We will create a GitHub Security Advisory and assign a CVE identifier if necessary.
    *   We will release the patch and publish the advisory, giving you full credit for the discovery (unless you wish to remain anonymous).
    *   We typically aim for disclosure as soon as the patch is available to our users.

## Safe Harbor

We consider security research performed in good faith and in accordance with this policy to be authorized and beneficial. We will not initiate legal action against researchers for accidentally violating this policy as long as they make a good faith effort to comply. This includes:

*   Not accessing, modifying, or destroying user data.
*   Not causing a service interruption or degradation of our systems.
*   Promptly reporting the vulnerability to us.

Thank you for your help in keeping ForexSignalBot secure and reliable for everyone. Your contributions are invaluable to the success and safety of this project.
