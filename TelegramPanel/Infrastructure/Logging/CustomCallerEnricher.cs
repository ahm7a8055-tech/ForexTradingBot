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
                string typeName = method.DeclaringType.FullName ?? string.Empty;
                string assemblyName = method.DeclaringType.Assembly.GetName().Name ?? string.Empty;

                // Skip excluded namespaces
                if (_excludedNamespaces.Any(prefix => ns.StartsWith(prefix)))
                    continue;
                // Skip known logging and infrastructure types
                if (typeName.Contains("CustomCallerEnricher") ||
                    typeName.Contains("Serilog") ||
                    typeName.Contains("Microsoft.Extensions.Logging") ||
                    typeName.Contains("System.Runtime") ||
                    typeName.Contains("System.Threading"))
                    continue;
                // Skip known logging and infrastructure assemblies
                if (assemblyName.StartsWith("Serilog") ||
                    assemblyName.StartsWith("Microsoft.") ||
                    assemblyName.StartsWith("System."))
                    continue;
                // Skip compiler-generated/async state machine frames
                if (typeName.Contains("<") || typeName.Contains(">") || method.Name == "MoveNext")
                    continue;

                // Found relevant frame
                int line = frame.GetFileLineNumber();
                string? file = frame.GetFileName();
                if (line == 0)
                    line = frame.GetILOffset();
                return (method, file, line);
            }
            // Fallback: no suitable frame found
            return (null, null, 0);
        }
    }
}
