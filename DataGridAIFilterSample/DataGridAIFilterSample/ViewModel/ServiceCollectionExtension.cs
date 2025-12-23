namespace DataGridAIFilterSample;

/// <summary>
/// Provides extension methods for registering AI-related services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers AI settings and related services (filter service, view model, and main page) into the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the AI services to.</param>
    /// <returns>The updated service collection with AI services registered.</returns>
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        services.AddSingleton<IAiFilterService, AiFilterService>();
        services.AddSingleton<EmployeesViewModel>();
        services.AddTransient<MainPage>();
        return services;
    }
}
