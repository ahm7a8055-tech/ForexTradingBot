using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Resources;

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
            foreach (string lang in SupportedLanguages)
            {
                _ = new CultureInfo(lang);
                ResourceManager rm = new(_baseName, _assembly);
                _resourceManagers[lang] = rm;
            }
        }

        public string GetString(string key, string? langCode = null)
        {
            langCode = NormalizeLang(langCode);
            if (!_resourceManagers.TryGetValue(langCode, out ResourceManager? rm))
            {
                rm = _resourceManagers["fa"];
            }

            string? value = rm.GetString(key, new CultureInfo(langCode));
            if (string.IsNullOrEmpty(value) && langCode != "fa")
            {
                value = _resourceManagers["fa"].GetString(key, new CultureInfo("fa"));
            }

            return value ?? key;
        }

        private static string NormalizeLang(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return "fa";
            }

            lang = lang.ToLowerInvariant();
            return lang.StartsWith("fa") ? "fa" : lang.StartsWith("ru") ? "ru" : "fa";
        }
    }
    #endregion
}