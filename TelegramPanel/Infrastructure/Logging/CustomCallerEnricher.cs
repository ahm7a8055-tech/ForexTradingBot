// --- File: Infrastructure/Logging/CustomCallerEnricher.cs ---

using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Reflection;

namespace TelegramPanel.Infrastructure.Logging
{
    /// <summary>
    /// A powerful custom Serilog enricher that extracts the actual caller method
    /// from the call stack, intelligently skipping framework or irrelevant namespaces.
    /// </summary>
    public class CustomCallerEnricher : ILogEventEnricher
    {
        private readonly string[] _excludedNamespaces;

        /// <summary>
        /// Initializes a new instance of <see cref="CustomCallerEnricher"/>.
        /// </summary>
        /// <param name="excludedNamespaces">
        /// An array of namespace prefixes to exclude while walking up the stack trace.
        /// </param>
        public CustomCallerEnricher(params string[] excludedNamespaces)
        {
            _excludedNamespaces = excludedNamespaces ?? Array.Empty<string>();
        }

        /// <inheritdoc />
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var (method, filePath, lineNumber) = FindActualCaller();

            if (method is not null)
            {
                string caller = $"{method.DeclaringType?.FullName}.{method.Name}() in {filePath ?? "UnknownFile"}:line {lineNumber}";
                LogEventProperty property = propertyFactory.CreateProperty("Caller", caller);
                logEvent.AddPropertyIfAbsent(property);
            }
        }

        private (MethodBase? method, string? filePath, int lineNumber) FindActualCaller()
        {
            StackTrace trace = new(true);

            foreach (var frame in trace.GetFrames() ?? Array.Empty<StackFrame>())
            {
                MethodBase? method = frame.GetMethod();
                if (method?.DeclaringType == null)
                    continue;

                string ns = method.DeclaringType.Namespace ?? string.Empty;

                if (_excludedNamespaces.Any(prefix => ns.StartsWith(prefix)))
                    continue;

                // Found relevant frame
                int line = frame.GetFileLineNumber();
                string? file = frame.GetFileName();

                // If no PDBs are present, fallback to IL offset line numbers
                if (line == 0)
                    line = frame.GetILOffset();

                return (method, file, line);
            }

            return (null, null, 0);
        }
    }
}
