using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;
using SupportBot.App.ViewModels;
using Windows.Storage;

namespace SupportBot.App;

/// <summary>
/// Provides extension methods for configuring dependency injection services.
/// </summary>
internal static class DependencyExtensions
{
    /// <summary>
    /// Configures the application's dependency injection services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    internal static IServiceCollection Configure(this IServiceCollection services)
    {
        services.AddSerilogLogging();
        services.AddViewModels();
        return services;
    }

    /// <summary>
    /// Adds and configures Serilog logging for the application.
    /// </summary>
    /// <param name="services">The service collection to add logging to.</param>
    /// <returns>The service collection with Serilog logging configured.</returns>
    internal static IServiceCollection AddSerilogLogging(this IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.File(
                    path: Path.Combine(
                        ApplicationData.Current.LocalFolder.Path,
                        "logs",
                        "logs/log-.json"
                    ),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    formatter: new JsonFormatter()
                )
                .CreateLogger();
            logging.AddSerilog(Log.Logger);
        });
        return services;
    }

    /// <summary>
    /// Registers all ViewModel services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add ViewModels to.</param>
    /// <returns>The service collection with ViewModels registered.</returns>
    internal static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        return services;
    }
}
