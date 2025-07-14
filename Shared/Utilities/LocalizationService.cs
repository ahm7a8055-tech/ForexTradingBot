using System.Globalization;
using System.Resources;
using System.Reflection;
using System.Collections.Concurrent;

namespace Shared.Utilities
{
    #region Interface
    public interface ILocalizationService
    {
        string GetString(string key, string? langCode = null);
    }
    #endregion

    #region Implementation
    public class LocalizationService : ILocalizationService
    {
        private static readonly string[] SupportedLanguages = ["fa", "en", "ru"];
        private readonly ConcurrentDictionary<string, ResourceManager> _resourceManagers = new();
        private readonly string _baseName = "Shared.Resources.StartMenu";
        private readonly Assembly _assembly;

        public LocalizationService()
        {
            _assembly = typeof(LocalizationService).Assembly;
            foreach (var lang in SupportedLanguages)
            {
                var culture = new CultureInfo(lang);
                var rm = new ResourceManager(_baseName, _assembly);
                _resourceManagers[lang] = rm;
            }
        }

        public string GetString(string key, string? langCode = null)
        {
            langCode = NormalizeLang(langCode);
            if (!_resourceManagers.TryGetValue(langCode, out var rm))
                rm = _resourceManagers["fa"];
            var value = rm.GetString(key, new CultureInfo(langCode));
            if (string.IsNullOrEmpty(value) && langCode != "fa")
                value = _resourceManagers["fa"].GetString(key, new CultureInfo("fa"));
            return value ?? key;
        }

        private static string NormalizeLang(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "fa";
            lang = lang.ToLowerInvariant();
            if (lang.StartsWith("fa")) return "fa";
            if (lang.StartsWith("ru")) return "ru";
            return "fa";
        }
    }
    #endregion
} 