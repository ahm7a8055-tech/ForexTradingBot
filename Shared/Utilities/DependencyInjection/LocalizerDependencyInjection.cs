using Microsoft.Extensions.DependencyInjection;

namespace Shared.Utilities.DependencyInjection
{
    #region Pro Localization Dependency Injection
    public static class LocalizerDependencyInjection
    {
        public static IServiceCollection AddProLocalization(this IServiceCollection services)
        {
            // Register the localization service as singleton
            _ = services.AddSingleton<ILocalizationService, LocalizationService>();
            return services;
        }
    }
    #endregion
}