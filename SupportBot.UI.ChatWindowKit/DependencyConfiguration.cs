using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace SupportBot.UI.ChatWindowKit;

/// <summary>
/// Provides centralized configuration and retrieval of application services and the main window instance.
/// </summary>
/// <remarks>
/// This static façade mirrors assignments to the corresponding orchestrator layer (Assistants.Orchestrator.DependencyConfiguration)
/// so that UI layer code can remain decoupled from deeper orchestrator concerns.
/// Thread-safety: This class performs no synchronization; configure and assign properties during application startup (single-threaded).
/// </remarks>
public static class DependencyConfiguration
{
    /// <summary>
    /// Backing field for <see cref="MainWindow"/>.
    /// </summary>
    private static Window? _mainWindow;

    /// <summary>
    /// Backing field for <see cref="Services"/>.
    /// </summary>
    private static ServiceProvider? _services;

    /// <summary>
    /// Gets or sets the application's main <see cref="Window"/>.
    /// </summary>
    /// <remarks>
    /// Setting this property also propagates the value to <c>Assistants.Orchestrator.DependencyConfiguration.MainWindow</c>
    /// ensuring a single authoritative window reference across layers.
    /// </remarks>
    public static Window? MainWindow
    {
        get => _mainWindow;
        set
        {
            _mainWindow = value;
            Assistants.Orchestrator.DependencyConfiguration.MainWindow = value;
        }
    }

    /// <summary>
    /// Gets or sets the root <see cref="ServiceProvider"/> used for dependency resolution.
    /// </summary>
    /// <remarks>
    /// Setting this property cascades the provider to the orchestrator layer. It should be assigned
    /// exactly once after calling <see cref="Configure(ServiceCollection)"/> and building the provider.
    /// </remarks>
    public static ServiceProvider? Services
    {
        get => _services;
        set
        {
            _services = value;
            Assistants.Orchestrator.DependencyConfiguration.Services = value;
        }
    }

    /// <summary>
    /// Configures dependency injection by delegating to the orchestrator configuration and applying UI-specific registrations.
    /// </summary>
    /// <param name="services">The mutable <see cref="ServiceCollection"/> to add services to.</param>
    /// <remarks>
    /// This method should be invoked early during application startup before building the <see cref="ServiceProvider"/>.
    /// After configuration, assign the built provider to <see cref="Services"/>.
    /// </remarks>
    public static void Configure(ServiceCollection services)
    {
        Assistants.Orchestrator.DependencyConfiguration.Configure(services);
        services.Configure();
    }

    /// <summary>
    /// Resolves a required service of the specified type from the configured <see cref="ServiceProvider"/>.
    /// </summary>
    /// <typeparam name="T">Concrete or interface type of the service to resolve.</typeparam>
    /// <returns>The resolved service instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Services"/> is <c>null</c>, indicating dependency injection has not been initialized.
    /// </exception>
    /// <remarks>
    /// This is a convenience wrapper over <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}(IServiceProvider)"/>.
    /// Prefer constructor injection where practical; use this only in static contexts or integration points that
    /// cannot leverage DI directly.
    /// </remarks>
    public static T GetService<T>()
        where T : class
    {
        if (Services == null)
        {
            throw new InvalidOperationException("Services is null");
        }

        return Services.GetRequiredService<T>();
    }
}
