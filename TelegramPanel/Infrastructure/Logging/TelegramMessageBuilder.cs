// --- File: Infrastructure/Logging/TelegramMessageBuilder.cs (V9.0 - POWERFUL UPGRADE) ---

using Serilog.Events;
using Shared.Security; // For enhanced exception sanitization
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramPanel.Infrastructure.Logging
{
    /// <summary>
    /// 🚀 POWERFUL UPGRADE: Enhanced intelligent "brain" of the God Mode sink with advanced error analysis,
    /// detailed debugging information, and smart categorization while maintaining security.
    /// This version provides comprehensive error details while protecting sensitive data.
    /// </summary>
    public class TelegramMessageBuilder
    {
        #region Enhanced Regex Definitions
        private static readonly Regex CallerRegex = new(@"^(?<method>.*) in (?<path>.*?):line (?<line>\d+)$", RegexOptions.Compiled);
        private static readonly Regex SensitiveDataRegex = new(@"(password|token|secret|key|auth|bearer|credential)s?[""':=\s]+[\w\-.~/+=]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HttpErrorRegex = new(@"(status code|StatusCode)\s?(\d{3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExceptionTypeRegex = new(@"^([A-Za-z_][A-Za-z0-9_]*Exception)", RegexOptions.Compiled);
        private static readonly Regex ErrorCodeRegex = new(@"(?:Error Code|Code):\s*([A-Z]+-\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        #endregion

        private readonly string _dashboardUrl;
        private readonly IExceptionSanitizer _exceptionSanitizer;

        public TelegramMessageBuilder(string dashboardUrl)
        {
            _dashboardUrl = dashboardUrl;
            _exceptionSanitizer = new ExceptionSanitizer(); // Use enhanced sanitizer
        }

        public (string message, Telegram.Bot.Types.ReplyMarkups.ReplyMarkup keyboard) Build(LogEvent logEvent, int occurrenceCount)
        {
            StringBuilder sb = new();

            BuildEnhancedHeader(sb, logEvent, occurrenceCount);
            BuildEnhancedMessageAndException(sb, logEvent);
            BuildEnhancedCodeLocation(sb, logEvent);
            BuildEnhancedSystemContext(sb, logEvent);
            BuildEnhancedIntelligentAnalysis(sb, logEvent);
            BuildEnhancedTimestamp(sb, logEvent);

            string sanitizedMessage = ApplySmartSanitization(sb.ToString());
            Telegram.Bot.Types.ReplyMarkups.ReplyMarkup keyboard = BuildActionButtons(logEvent);

            return (sanitizedMessage, keyboard);
        }

        #region 🆕 NEW: Enhanced Message Building Components
        private void BuildEnhancedHeader(StringBuilder sb, LogEvent logEvent, int occurrenceCount)
        {
            string env = GetProperty(logEvent, "EnvironmentName");
            string headerEmoji = env.Equals("Production", StringComparison.OrdinalIgnoreCase) ? "🚨🔥🚨" : "🔬🐞🔬";

            // 🆕 NEW: Enhanced level formatting
            string formattedLevel = FormatLevelToString(logEvent.Level);
            string severityEmoji = GetSeverityEmoji(logEvent.Level);

            _ = sb.AppendLine($"{headerEmoji} *{severityEmoji} {formattedLevel} | {GetProperty(logEvent, "Application")}*");

            // 🆕 NEW: Enhanced occurrence tracking
            if (occurrenceCount > 1)
            {
                string occurrenceEmoji = occurrenceCount > 5 ? "🚨" : occurrenceCount > 3 ? "⚠️" : "🔄";
                _ = sb.AppendLine($"*{occurrenceEmoji} This error has now occurred {occurrenceCount} times!*");
            }
            _ = sb.AppendLine();
        }

        private void BuildEnhancedMessageAndException(StringBuilder sb, LogEvent logEvent)
        {
            _ = sb.AppendLine("📄 *Message*");
            string message = logEvent.RenderMessage();

            // 🆕 NEW: Enhanced message analysis
            if (logEvent.Exception != null)
            {
                // Use enhanced sanitizer for better error details
                string enhancedExceptionInfo = _exceptionSanitizer.Sanitize(logEvent.Exception, includeHash: true);

                // Extract key information from enhanced sanitizer output
                (string ExceptionType, string Category, string Severity, string ErrorCode, string SanitizedMessage) = AnalyzeEnhancedExceptionInfo(enhancedExceptionInfo);

                _ = sb.AppendLine($"`{message}`");
                _ = sb.AppendLine();

                _ = sb.AppendLine("💣 *Exception Details*");
                _ = sb.AppendLine($"🔹 Type: `{ExceptionType}`");
                _ = sb.AppendLine($"🔹 Category: `{Category}`");
                _ = sb.AppendLine($"🔹 Severity: {Severity}");
                _ = sb.AppendLine($"🔹 Error Code: `{ErrorCode}`");

                // 🆕 NEW: Show sanitized exception message
                if (!string.IsNullOrEmpty(SanitizedMessage))
                {
                    _ = sb.AppendLine($"🔹 Message: `{SanitizedMessage}`");
                }
            }
            else
            {
                _ = sb.AppendLine($"`{message}`");
            }
            _ = sb.AppendLine();
        }

        private void BuildEnhancedCodeLocation(StringBuilder sb, LogEvent logEvent)
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
            string methodName = match.Groups["method"].Value;

            _ = sb.AppendLine($"📂 File: `{Path.GetFileName(filePath)}`");
            _ = sb.AppendLine($"#️⃣ Line: `{line}`");
            _ = sb.AppendLine($"🔧 Method: `{methodName}`");

            // 🆕 NEW: Enhanced code snippet with better context
            string codeSnippet = ExtractEnhancedCodeSnippet(filePath, line);
            if (!string.IsNullOrEmpty(codeSnippet))
            {
                _ = sb.AppendLine("\n```csharp\n" + codeSnippet + "\n```");
            }
            _ = sb.AppendLine();
        }

        private void BuildEnhancedSystemContext(StringBuilder sb, LogEvent logEvent)
        {
            _ = sb.AppendLine("🌐 *System & Request Context*");
            _ = sb.AppendLine($"📍 Environment: `{GetProperty(logEvent, "EnvironmentName")}`");
            _ = sb.AppendLine($"💻 Machine: `{GetProperty(logEvent, "MachineName")}`");
            _ = sb.AppendLine($"🔗 Request ID: `{GetProperty(logEvent, "RequestId") ?? "N/A"}`");

            // 🆕 NEW: Additional context information
            string processId = Environment.ProcessId.ToString();
            _ = sb.AppendLine($"🆔 Process ID: `{processId}`");
            _ = sb.AppendLine($"⏰ Time: `{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC`");
            _ = sb.AppendLine();
        }

        private void BuildEnhancedIntelligentAnalysis(StringBuilder sb, LogEvent logEvent)
        {
            if (logEvent.Exception == null)
            {
                return;
            }

            _ = sb.AppendLine("🤖 *AI-Powered Analysis*");

            // 🆕 NEW: Use enhanced sanitizer for intelligent analysis
            string enhancedExceptionInfo = _exceptionSanitizer.Sanitize(logEvent.Exception, includeHash: false);
            (List<string> Suggestions, List<string> ErrorPatterns) = ExtractIntelligentSuggestions(enhancedExceptionInfo);

            foreach (string suggestion in Suggestions)
            {
                _ = sb.AppendLine($"- {suggestion}");
            }

            // 🆕 NEW: Add error patterns and trends
            if (ErrorPatterns.Any())
            {
                _ = sb.AppendLine();
                _ = sb.AppendLine("📊 *Error Patterns Detected*");
                foreach (string pattern in ErrorPatterns)
                {
                    _ = sb.AppendLine($"- {pattern}");
                }
            }
            _ = sb.AppendLine();
        }

        private void BuildEnhancedTimestamp(StringBuilder sb, LogEvent logEvent)
        {
            _ = sb.AppendLine($"🕰️ *Timestamp (UTC):* `{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff}`");
        }
        #endregion

        #region 🆕 NEW: Enhanced Utility Methods
        private string GetSeverityEmoji(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => "🔵",
                LogEventLevel.Debug => "🔵",
                LogEventLevel.Information => "🟢",
                LogEventLevel.Warning => "🟡",
                LogEventLevel.Error => "🔴",
                LogEventLevel.Fatal => "💀",
                _ => "⚪"
            };
        }

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

        private string ApplySmartSanitization(string message)
        {
            // 🆕 NEW: Smarter sanitization that preserves important error details
            string sanitized = message;

            // Only redact truly sensitive patterns, not entire error messages
            sanitized = SensitiveDataRegex.Replace(sanitized, "$1: \"[REDACTED]\"");

            // Preserve important error information
            sanitized = sanitized.Replace("[EMPTY_MESSAGE]", "No message available");
            sanitized = sanitized.Replace("[NULL_EXCEPTION]", "Exception object was null");

            return sanitized;
        }

        private string ExtractEnhancedCodeSnippet(string filePath, int errorLine, int contextLines = 3)
        {
            if (!File.Exists(filePath))
            {
                return "// File not found or not accessible";
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
                    string lineNumber = $"{i + 1:D3}";
                    _ = sb.AppendLine($"{linePrefix}{lineNumber}: {lines[i]}");
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"// Error reading file: {ex.Message}";
            }
        }

        private Telegram.Bot.Types.ReplyMarkups.ReplyMarkup BuildActionButtons(LogEvent logEvent)
        {
            List<InlineKeyboardButton> buttons = [];
            if (!string.IsNullOrEmpty(_dashboardUrl))
            {
                string url = _dashboardUrl;
                if (logEvent.Properties.TryGetValue("RequestId", out LogEventPropertyValue? reqId))
                {
                    url += Uri.EscapeDataString(reqId.ToString("l", null));
                }
                buttons.Add(InlineKeyboardButton.WithUrl("🔍 Open Dashboard", url));
            }

            // 🆕 NEW: Add action buttons based on error type
            if (logEvent.Exception != null)
            {
                string exceptionType = logEvent.Exception.GetType().Name;
                if (exceptionType.Contains("Database") || exceptionType.Contains("Sql"))
                {
                    buttons.Add(InlineKeyboardButton.WithUrl("🗄️ Database Status", $"{_dashboardUrl}/database"));
                }
                if (exceptionType.Contains("Http") || exceptionType.Contains("Network"))
                {
                    buttons.Add(InlineKeyboardButton.WithUrl("🌐 Network Status", $"{_dashboardUrl}/network"));
                }
            }

            return new InlineKeyboardMarkup(buttons);
        }
        #endregion

        #region 🆕 NEW: Enhanced Analysis Methods
        private (string ExceptionType, string Category, string Severity, string ErrorCode, string SanitizedMessage) AnalyzeEnhancedExceptionInfo(string enhancedExceptionInfo)
        {
            string[] lines = enhancedExceptionInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            string exceptionType = "Unknown";
            string category = "UNKNOWN_ERROR";
            string severity = "🔵 INFO";
            string errorCode = "GEN-0000";
            string sanitizedMessage = "";

            foreach (string line in lines)
            {
                if (line.Contains("Exception Type:"))
                {
                    exceptionType = ExtractValue(line, "Exception Type:");
                }
                else if (line.Contains("Error Code:"))
                {
                    errorCode = ExtractValue(line, "Error Code:");
                }
                else if (line.Contains("MESSAGE"))
                {
                    // Get the next line as the message
                    int messageIndex = Array.IndexOf(lines, line) + 1;
                    if (messageIndex < lines.Length)
                    {
                        sanitizedMessage = lines[messageIndex].Trim('`');
                    }
                }
            }

            // Extract category and severity from the header
            Match headerMatch = Regex.Match(enhancedExceptionInfo, @"🚨\s*(.+?)\s*\|\s*(.+?)(?:\r|\n|$)");
            if (headerMatch.Success)
            {
                severity = headerMatch.Groups[1].Value.Trim();
                category = headerMatch.Groups[2].Value.Trim();
            }

            return (exceptionType, category, severity, errorCode, sanitizedMessage);
        }

        private (List<string> Suggestions, List<string> ErrorPatterns) ExtractIntelligentSuggestions(string enhancedExceptionInfo)
        {
            List<string> suggestions = [];
            List<string> errorPatterns = [];

            string[] lines = enhancedExceptionInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            bool inSuggestions = false;

            foreach (string line in lines)
            {
                if (line.Contains("INTELLIGENT ANALYSIS"))
                {
                    inSuggestions = true;
                    continue;
                }
                else if (line.Contains("CONTEXT INFO") || line.Contains("Security Hash"))
                {
                    inSuggestions = false;
                    continue;
                }

                if (inSuggestions && line.Trim().StartsWith("-"))
                {
                    suggestions.Add(line.Trim());
                }
            }

            // Add dynamic patterns based on exception content
            if (enhancedExceptionInfo.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                errorPatterns.Add("🔗 Connection-related error detected");
            }
            if (enhancedExceptionInfo.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                errorPatterns.Add("⏱️ Timeout-related error detected");
            }
            if (enhancedExceptionInfo.Contains("permission", StringComparison.OrdinalIgnoreCase))
            {
                errorPatterns.Add("🔐 Permission-related error detected");
            }

            return (suggestions, errorPatterns);
        }

        private string ExtractValue(string line, string prefix)
        {
            int index = line.IndexOf(prefix);
            return index >= 0 ? line[(index + prefix.Length)..].Trim() : "";
        }
        #endregion
    }
}