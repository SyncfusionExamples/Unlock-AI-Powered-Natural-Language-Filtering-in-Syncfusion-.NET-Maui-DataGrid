using AiGridSample;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;

namespace DataGridSample
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureSyncfusionCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"); // or read from secure storage
            builder.Services.AddSingleton(new AiSettings
            {
                Provider = AiProvider.OpenAI,       // or AiProvider.AzureOpenAI
                OpenAiApiKey = openAiKey,
                OpenAiModel = "gpt-4o-mini",        // choose any chat model with JSON output
                                                    // For Azure OpenAI:
                AzureEndpoint = "",                 // https://your-resource.openai.azure.com/
                AzureApiKey = "",                   // your azure key
                AzureDeployment = ""                // your deployment name
            });

            builder.Services.AddSingleton<IAiFilterService, AiFilterService>();
            builder.Services.AddSingleton<EmployeesViewModel>();
            builder.Services.AddTransient<MainPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
