using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Text.Json;
using System.Text.Json.Serialization;
using Toxiq.WebApp.Client.Extensions;
using Toxiq.WebApp.Client.Services.Api;
using Toxiq.WebApp.Client.Services.Caching;
using Toxiq.WebApp.Client.Services.LazyLoading;
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
            builder.Services.AddScoped<IPlatformService, PlatformService>();
            builder.Services.AddScoped<ILocalSecureStorage, LocalSecureStorage>();

            // IndexedDB for all cached data
            builder.Services.AddScoped<ICacheService, IndexedDbCacheService>();


            // FIXED: HttpClient registration for WebAssembly
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

            // API Service registration
            builder.Services.AddScoped<OptimizedApiService>();
            builder.Services.AddScoped<IApiService>(provider => provider.GetRequiredService<OptimizedApiService>());
            builder.Services.AddApiServices();

            builder.Services.AddAuthenticationServices();
            builder.Services.AddUIServices();

            // Additional services
            builder.Services.AddScoped<ILazyLoader, LazyLoader>();

            // Add feed and API services via extension methods
            builder.Services.AddFeedServices();


            // FluentUI Components
            builder.Services.AddFluentUIComponents();

            var host = builder.Build();

            await host.RunAsync();
        }
    }
}