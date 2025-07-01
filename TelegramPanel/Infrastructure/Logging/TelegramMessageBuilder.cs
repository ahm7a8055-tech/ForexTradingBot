// --- File: Infrastructure/Logging/TelegramMessageBuilder.cs (V8.2 - FINAL FIX) ---

using Serilog.Events;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramPanel.Infrastructure.Logging
{
    /// <summary>
    /// The intelligent "brain" of the God Mode sink. This class is responsible for
    /// parsing, analyzing, and formatting log events into beautiful, actionable Telegram messages.
    /// </summary>
    public class TelegramMessageBuilder
    {
        #region Regex Definitions
        private static readonly Regex CallerRegex = new(@"^(?<method>.*) in (?<path>.*?):line (?<line>\d+)$", RegexOptions.Compiled);
        private static readonly Regex SensitiveDataRegex = new(@"(password|token|secret|key|auth|bearer|credential)s?[""':=\s]+[\w\-.~/+=]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HttpErrorRegex = new(@"(status code|StatusCode)\s?(\d{3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        #endregion

        private readonly string _dashboardUrl;

        public TelegramMessageBuilder(string dashboardUrl)
        {
            _dashboardUrl = dashboardUrl;
        }

        public (string message, Telegram.Bot.Types.ReplyMarkups.ReplyMarkup keyboard) Build(LogEvent logEvent, int occurrenceCount)
        {
            StringBuilder sb = new();

            BuildHeader(sb, logEvent, occurrenceCount);
            BuildMessageAndException(sb, logEvent);
            BuildCodeLocation(sb, logEvent);
            BuildSystemContext(sb, logEvent);
            BuildIntelligentAnalysis(sb, logEvent);
            BuildTimestamp(sb, logEvent);

            string sanitizedMessage = Sanitize(sb.ToString());
            Telegram.Bot.Types.ReplyMarkups.ReplyMarkup keyboard = BuildActionButtons(logEvent);

            return (sanitizedMessage, keyboard);
        }

        #region Message Building Components
        private void BuildHeader(StringBuilder sb, LogEvent logEvent, int occurrenceCount)
        {
            string env = GetProperty(logEvent, "EnvironmentName");
            string headerEmoji = env.Equals("Production", StringComparison.OrdinalIgnoreCase) ? "🚨🔥🚨" : "🔬🐞🔬";

            // --- THIS IS THE FIX ---
            // We now call a helper method to correctly format the level instead of using ":u3".
            string formattedLevel = FormatLevelToString(logEvent.Level);
            _ = sb.AppendLine($"{headerEmoji} *{formattedLevel} | {GetProperty(logEvent, "Application")}*");

            if (occurrenceCount > 1)
            {
                _ = sb.AppendLine($"*This error has now occurred {occurrenceCount} times!*");
            }
            _ = sb.AppendLine();
        }

        // The rest of the methods are unchanged, but included for completeness.
        private void BuildMessageAndException(StringBuilder sb, LogEvent logEvent)
        {
            _ = sb.AppendLine("📄 *Message*");
            _ = sb.AppendLine($"`{logEvent.RenderMessage()}`");

            if (logEvent.Exception != null)
            {
                _ = sb.AppendLine($"💣 *Exception:* `{logEvent.Exception.GetType().Name}`");
            }
            _ = sb.AppendLine();
        }

        private void BuildCodeLocation(StringBuilder sb, LogEvent logEvent)
        {
            string caller = GetProperty(logEvent, "Caller");
            Match match = CallerRegex.Match(caller ?? "");

            if (!match.Success)
            {
                return;
            }

            _ = sb.AppendLine("🗺️ *Code Location*");
            string filePath = match.Groups["path"].Value;
            int line = int.Parse(match.Groups["line"].Value);

            _ = sb.AppendLine($"📂 `File:` *{Path.GetFileName(filePath)}*");
            _ = sb.AppendLine($"#️⃣ `Line:` *{line}*");
            _ = sb.AppendLine($"🔧 `Method:` `{match.Groups["method"].Value}`");

            string codeSnippet = ExtractCodeSnippet(filePath, line);
            if (!string.IsNullOrEmpty(codeSnippet))
            {
                _ = sb.AppendLine("\n```csharp\n" + codeSnippet + "\n```");
            }
            _ = sb.AppendLine();
        }

        private void BuildSystemContext(StringBuilder sb, LogEvent logEvent)
        {
            _ = sb.AppendLine("🌐 *System & Request Context*");
            _ = sb.AppendLine($"📍 `Env:` {GetProperty(logEvent, "EnvironmentName")}");
            _ = sb.AppendLine($"💻 `Machine:` {GetProperty(logEvent, "MachineName")}");
            _ = sb.AppendLine($"🔗 `RequestId:` {GetProperty(logEvent, "RequestId") ?? "N/A"}");
            _ = sb.AppendLine();
        }

        private void BuildIntelligentAnalysis(StringBuilder sb, LogEvent logEvent)
        {
            if (logEvent.Exception == null)
            {
                return;
            }

            _ = sb.AppendLine("🤖 *Robo-Analyst Suggestions*");
            bool suggestionMade = false;

            if (logEvent.Exception is HttpRequestException)
            {
                Match match = HttpErrorRegex.Match(logEvent.Exception.Message);
                if (match.Success)
                {
                    _ = sb.AppendLine($"- 💡 Detected HTTP Error `{match.Groups[2].Value}`. Check endpoint availability, firewalls, and DNS.");
                    suggestionMade = true;
                }
            }

            string exType = logEvent.Exception.GetType().Name;
            if (exType.Contains("Npgsql") || exType.Contains("Sql"))
            {
                _ = sb.AppendLine("- 💡 This is a database error. Verify connection strings, database server status, and user permissions.");
                suggestionMade = true;
            }

            if (!suggestionMade)
            {
                _ = sb.AppendLine("- 💡 No specific suggestions. Please review the attached stack trace for details.");
            }
            _ = sb.AppendLine();
        }

        private void BuildTimestamp(StringBuilder sb, LogEvent logEvent)
        {
            _ = sb.AppendLine($"🕰️ `Timestamp (UTC):` {logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        }

        private Telegram.Bot.Types.ReplyMarkups.ReplyMarkup BuildActionButtons(LogEvent logEvent)
        {
            List<InlineKeyboardButton> buttons = new();
            if (!string.IsNullOrEmpty(_dashboardUrl))
            {
                string url = _dashboardUrl;
                if (logEvent.Properties.TryGetValue("RequestId", out LogEventPropertyValue? reqId))
                {
                    url += Uri.EscapeDataString(reqId.ToString("l", null));
                }
                buttons.Add(InlineKeyboardButton.WithUrl("🔍 Open Dashboard", url));
            }

            return new InlineKeyboardMarkup(buttons);
        }
        #endregion

        #region Utility Methods

        // --- NEW HELPER METHOD TO FIX THE BUG ---
        private string FormatLevelToString(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => "VRB",
                LogEventLevel.Debug => "DBG",
                LogEventLevel.Information => "INF",
                LogEventLevel.Warning => "WRN",
                LogEventLevel.Error => "ERR",
                LogEventLevel.Fatal => "FTL",
                _ => level.ToString().ToUpper()[..3]
            };
        }

        private string GetProperty(LogEvent logEvent, string name)
        {
            return logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? p) && p is ScalarValue sv ? sv.Value?.ToString() ?? "" : "";
        }

        private string Sanitize(string message)
        {
            return SensitiveDataRegex.Replace(message, "$1: \"[REDACTED]\"");
        }

        private string ExtractCodeSnippet(string filePath, int errorLine, int contextLines = 2)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                StringBuilder sb = new();

                int startLine = Math.Max(0, errorLine - contextLines - 1);
                int endLine = Math.Min(lines.Length - 1, errorLine + contextLines - 1);

                for (int i = startLine; i <= endLine; i++)
                {
                    string linePrefix = i == errorLine - 1 ? ">> " : "   ";
                    _ = sb.AppendLine($"{linePrefix}{lines[i]}");
                }
                return sb.ToString();
            }
            catch { return null; }
        }
        #endregion
    }
}