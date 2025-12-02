namespace DataGridAIFilteringSample;


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
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        services.AddSingleton(new AiSettings
        {
            Provider = AiProvider.OpenAI,
            OpenAiApiKey = openAiKey,
            OpenAiModel = " " // your model
        });

        services.AddSingleton<IAiFilterService, AiFilterService>();
        services.AddSingleton<EmployeesViewModel>();
        services.AddTransient<MainPage>();

        return services;
    }
}

/// <summary>
/// Provides a startup configuration class for registering AI-related services during application initialization.
/// </summary>
public static class Startup
{
    /// <summary>
    /// Configures and registers AI settings and related services into the provided service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static void ConfigureServices(IServiceCollection services)
    {
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        services.AddSingleton(new AiSettings
        {
            Provider = AiProvider.OpenAI,
            OpenAiApiKey = openAiKey,
            OpenAiModel = "gpt-4o-mini"
        });

        services.AddSingleton<IAiFilterService, AiFilterService>();
        services.AddSingleton<EmployeesViewModel>();
        services.AddTransient<MainPage>();
    }
}
