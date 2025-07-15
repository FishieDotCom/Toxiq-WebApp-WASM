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
using Toxiq.WebApp.Client.Services.LazyLoading;
using Toxiq.WebApp.Client.Services.Platform;
using TelegramApps.Blazor.Extensions;

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
            builder.Logging.AddFilter("Toxiq.WebApp.Client.Services.Notifications", LogLevel.Debug);
            builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);


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

            // FIXED: HttpClient registration for WebAssembly
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

            // API Service registration
            builder.Services.AddScoped<OptimizedApiService>();
            builder.Services.AddScoped<IApiService>(provider => provider.GetRequiredService<OptimizedApiService>());

            // Authentication services - FIXED: Ensure proper order
            builder.Services.AddScoped<IAuthenticationProvider, TelegramAuthProvider>();
            builder.Services.AddScoped<IAuthenticationProvider, ManualAuthProvider>();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

            // Notification services - FIXED: Register in correct order
            builder.Services.AddScoped<INotificationService, NotificationService>();


            var baseUrl = builder.Configuration["ApiBaseUrl"]?.TrimEnd('/').Replace("/api", "") ?? "https://toxiq.xyz";
            builder.Services.AddSignalRHubGateway(baseUrl);


            builder.Services.AddChatServices();


            builder.Services.ConfigureChatOptions(options =>
            {
                options.DefaultPageSize = 20;
                options.MaxMessageLength = 500;
                options.EnableRealTimeNotifications = true;
                options.EnableBrowserNotifications = true;
                options.AutoScrollToNewMessages = true;
                options.ShowTypingIndicators = true;
                options.MessageEditTimeLimitMinutes = 15;
                options.EnableMessageReactions = true;
                options.EnableFileAttachments = true;
                options.MaxFileSize = 10 * 1024 * 1024; // 10MB
            });

            // Additional services
            builder.Services.AddScoped<ILazyLoader, LazyLoader>();

            // Add feed and API services via extension methods
            builder.Services.AddFeedServices();
            builder.Services.AddApiServices();

            // FluentUI Components
            builder.Services.AddFluentUIComponents();

            // Add Telegram WebApp services
            builder.Services.AddTelegramWebApp();

            var host = builder.Build();

            await host.RunAsync();
        }
    }
}