using Microsoft.Extensions.DependencyInjection;

namespace SupportBot.UI.ChatWindowKit;

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
        services.AddServices();
        services.AddViewModels();
        return services;
    }

    /// <summary>
    /// Registers application services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection with services registered.</returns>
    internal static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Registers all ViewModel services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add ViewModels to.</param>
    /// <returns>The service collection with ViewModels registered.</returns>
    internal static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services;
    }
}
