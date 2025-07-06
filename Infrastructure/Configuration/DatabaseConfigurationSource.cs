using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection; // For IServiceProvider
using System;

namespace Infrastructure.Configuration
{
    public class DatabaseConfigurationSource : IConfigurationSource
    {
        private readonly IServiceProvider _serviceProvider; // To resolve services needed by the provider
        private readonly Action<IDynamicConfigurationService, IConfigurationBuilder> _registerDefinedSettings;


        // We need a way to get IDynamicConfigurationService. Since it's registered as a singleton,
        // we can resolve it later via a temporary service provider or pass a factory.
        // For simplicity in registration, we might pass the service provider built from initial services.
        public DatabaseConfigurationSource(IServiceProvider serviceProvider, Action<IDynamicConfigurationService, IConfigurationBuilder> registerDefinedSettings)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _registerDefinedSettings = registerDefinedSettings ?? throw new ArgumentNullException(nameof(registerDefinedSettings));
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            // Create a scope to resolve scoped services if necessary, though IDynamicConfigurationService is singleton.
            // It's generally safer to resolve from a scope if there's any doubt.
            // However, configuration providers are often created very early.
            // For singletons, direct resolution from root provider is usually fine.

            var dynamicConfigService = _serviceProvider.GetRequiredService<IDynamicConfigurationService>();
            var initialConfigurationBuilder = new ConfigurationBuilder(); // Temporary builder to get appsettings values

            // Add existing sources from the main builder to ensure we can read appsettings.json for defaults
            foreach (var source in builder.Sources)
            {
                if (source is not DatabaseConfigurationSource) // Avoid recursion
                {
                    initialConfigurationBuilder.Add(source);
                }
            }
            var tempConfig = initialConfigurationBuilder.Build();

            // Register defined settings using the temporary configuration to get defaults
            // This ensures DynamicConfigurationService is aware of all settings and their JSON defaults
            // before its values are loaded by the DatabaseConfigurationProvider.
            // This is a bit of a chicken-and-egg problem solver.
            _registerDefinedSettings(dynamicConfigService, initialConfigurationBuilder);


            return new DatabaseConfigurationProvider(dynamicConfigService, tempConfig);
        }
    }
}
