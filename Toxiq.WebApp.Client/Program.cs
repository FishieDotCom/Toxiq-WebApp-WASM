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

            builder.Services.AddBlazoredLocalStorage(config =>
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
            builder.Services.AddScoped<ICacheService, MultiLayerCacheService>();

            // Token Storage
            builder.Services.AddScoped<ITokenStorage, LocalStorageTokenStorage>();

            // Platform Detection
            builder.Services.AddScoped<IPlatformService, PlatformService>();

            // API Services
            builder.Services.AddScoped<IApiService, OptimizedApiService>();

            // Authentication
            builder.Services.AddScoped<IAuthenticationProvider, TelegramAuthProvider>();
            builder.Services.AddScoped<IAuthenticationProvider, ManualAuthProvider>();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();





            // Lazy loading assemblies
            builder.Services.AddScoped<ILazyLoader, LazyLoader>();

            // FluentUI Components
            builder.Services.AddFluentUIComponents();

            // Logging
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            await builder.Build().RunAsync();
        }
    }
}
