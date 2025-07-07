using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Text.Json;
using System.Text.Json.Serialization;
using Toxiq.WebApp.Client.Extensions;
using Toxiq.WebApp.Client.Services.Api;
using Toxiq.WebApp.Client.Services.Authentication;
using Toxiq.WebApp.Client.Services.Caching;
using Toxiq.WebApp.Client.Services.JavaScript;
using Toxiq.WebApp.Client.Services.LazyLoading;
using Toxiq.WebApp.Client.Services.Notifications;
using Toxiq.WebApp.Client.Services.Platform;

namespace Toxiq.WebApp.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            // Configure logging first
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Get API configuration
            var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://toxiq.xyz/api/";

            // Configure LocalStorage with JSON options
            builder.Services.AddBlazoredLocalStorageAsSingleton(config =>
            {
                config.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                config.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                config.JsonSerializerOptions.IgnoreReadOnlyProperties = true;
                config.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                config.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                config.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
                config.JsonSerializerOptions.WriteIndented = false;
            });

            // Core services (no dependencies)
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<ICacheService, MultiLayerCacheService>();
            builder.Services.AddSingleton<ITokenStorage, LocalStorageTokenStorage>();
            builder.Services.AddScoped<IPlatformService, PlatformService>();
            builder.Services.AddSingleton<IIndexedDbService, IndexedDbService>();

            // FIXED: HttpClient registration for WebAssembly - use the default HttpClient registration
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

            // API Service registration - use scoped to match HttpClient lifetime
            builder.Services.AddScoped<OptimizedApiService>();
            builder.Services.AddScoped<IApiService>(provider => provider.GetRequiredService<OptimizedApiService>());

            // Authentication services - make these scoped to work with scoped API service
            builder.Services.AddScoped<IAuthenticationProvider, TelegramAuthProvider>();
            builder.Services.AddScoped<IAuthenticationProvider, ManualAuthProvider>();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<ITelegramAuthJsInvoker, TelegramAuthJsInvoker>();

            // Notification service - make scoped to work with scoped API service
            builder.Services.AddScoped<INotificationService, NotificationService>();

            // SignalR service - make scoped to work with other scoped services
            builder.Services.AddScoped<ISignalRService, SignalRService>();

            // Additional services
            builder.Services.AddScoped<ILazyLoader, LazyLoader>();

            // Add feed and API services via extension methods
            builder.Services.AddFeedServices();
            builder.Services.AddApiServices();

            // FluentUI Components
            builder.Services.AddFluentUIComponents();

            var host = builder.Build();

            // Initialize notification services after app is built
            try
            {
                using var scope = host.Services.CreateScope();
                var signalRService = scope.ServiceProvider.GetRequiredService<ISignalRService>();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                // Start SignalR if authenticated
                if (authService.IsAuthenticated().GetValueOrDefault(false))
                {
                    await signalRService.StartAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting notifications: {ex.Message}");
            }

            await host.RunAsync();
        }
    }
}