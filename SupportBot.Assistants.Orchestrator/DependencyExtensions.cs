using System;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace SupportBot.Assistants.Orchestrator;

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
        services.AddSingleton<IMainAgent, MainAgent>(services =>
        {
            var assistantId =
                Environment.GetEnvironmentVariable("MAIN_AGENT_ID")
                ?? throw new InvalidOperationException(
                    "MAIN_AGENT_ID environment variable is not set."
                );
            var openAIClient = services.GetRequiredService<OpenAIClient>();
            return new MainAgent(openAIClient, assistantId);
        });
        return services;
    }

    /// <summary>
    /// Registers application services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection with services registered.</returns>
    internal static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton(serviceProvider => new OpenAIClient(
            apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ));
        return services;
    }
}
