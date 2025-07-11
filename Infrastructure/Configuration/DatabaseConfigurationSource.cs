using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection; // For IServiceCollection
using System;

namespace Infrastructure.Configuration
{
    public class DatabaseConfigurationSource : IConfigurationSource
    {
        private readonly IServiceCollection _services; // To build service provider when needed
        private readonly Action<IDynamicConfigurationService, IConfigurationBuilder> _registerDefinedSettings;


        // We need a way to get IDynamicConfigurationService. Since it's registered as a singleton,
        // we can resolve it later via a temporary service provider or pass a factory.
        // For simplicity in registration, we might pass the service collection and build provider when needed.
        public DatabaseConfigurationSource(IServiceCollection services, Action<IDynamicConfigurationService, IConfigurationBuilder> registerDefinedSettings)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _registerDefinedSettings = registerDefinedSettings ?? throw new ArgumentNullException(nameof(registerDefinedSettings));
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            // Build a temporary service provider only when needed for this specific operation
            // This avoids the ASP0000 warning about calling BuildServiceProvider from application code
            using var tempServiceProvider = _services.BuildServiceProvider();

            var dynamicConfigService = tempServiceProvider.GetRequiredService<IDynamicConfigurationService>();
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
