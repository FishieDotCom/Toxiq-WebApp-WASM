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

            //builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Logging.SetMinimumLevel(LogLevel.Information);
            var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://toxiq.xyz/api/";
            builder.Services.AddToxiqServices(apiBaseUrl);
            builder.Services.AddFeedServices();
            builder.Services.AddApiServices();

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

            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<ICacheService, MultiLayerCacheService>();

            // Token Storage
            builder.Services.AddSingleton<ITokenStorage, LocalStorageTokenStorage>();

            // Platform Detection
            builder.Services.AddScoped<IPlatformService, PlatformService>();

            // API Services

            // Authentication
            builder.Services.AddSingleton<IAuthenticationProvider, TelegramAuthProvider>();
            builder.Services.AddSingleton<IAuthenticationProvider, ManualAuthProvider>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

            builder.Services.AddScoped<ITelegramAuthJsInvoker, TelegramAuthJsInvoker>();

            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddSingleton<IIndexedDbService, IndexedDbService>();
            builder.Services.AddSingleton<ISignalRService, SignalRService>();

            builder.Services.AddSingleton<IApiService, OptimizedApiService>();


            builder.Services.AddSingleton<ISignalRService>(provider =>
            {
                var authService = provider.GetRequiredService<IAuthenticationService>();
                var notificationService = provider.GetRequiredService<INotificationService>();
                var configuration = provider.GetRequiredService<IConfiguration>();
                var logger = provider.GetRequiredService<ILogger<SignalRService>>();

                return new SignalRService(authService, notificationService, configuration, logger);
            });


            // Lazy loading assemblies
            builder.Services.AddScoped<ILazyLoader, LazyLoader>();

            // FluentUI Components
            builder.Services.AddFluentUIComponents();

            // Logging
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            var host = builder.Build();

            // Initialize notification services
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
