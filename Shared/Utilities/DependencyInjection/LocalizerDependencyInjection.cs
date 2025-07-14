using Microsoft.Extensions.DependencyInjection;
using Shared.Utilities;

namespace Shared.Utilities.DependencyInjection
{
    #region Pro Localization Dependency Injection
    public static class LocalizerDependencyInjection
    {
        public static IServiceCollection AddProLocalization(this IServiceCollection services)
        {
            // Register the localization service as singleton
            services.AddSingleton<ILocalizationService, LocalizationService>();
            return services;
        }
    }
    #endregion
} 