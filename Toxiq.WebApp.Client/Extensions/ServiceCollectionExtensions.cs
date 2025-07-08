// Toxiq.WebApp.Client/Extensions/ServiceCollectionExtensions.cs
using Microsoft.FluentUI.AspNetCore.Components;
using Toxiq.WebApp.Client.Services.Api;
using Toxiq.WebApp.Client.Services.Authentication;
using Toxiq.WebApp.Client.Services.Core;
using Toxiq.WebApp.Client.Services.Feed;
using Toxiq.WebApp.Client.Services.JavaScript;

namespace Toxiq.WebApp.Client.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all Toxiq services following mobile app architecture patterns
        /// </summary>
        public static IServiceCollection AddToxiqServices(this IServiceCollection services, string baseUrl)
        {
            // Configure HttpClient for API calls (mirrors mobile app HTTP configuration)
            services.AddHttpClient("ToxiqApi", client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("User-Agent", "Toxiq-WebApp/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Add authentication services (mirrors mobile auth architecture)
            services.AddAuthenticationServices();

            // Add API services (mirrors mobile API service layer)
            services.AddApiServices();

            // Add feed services (mirrors mobile feed management)
            services.AddFeedServices();

            // Add UI component services
            services.AddUIServices();

            // Add performance services
            services.AddPerformanceServices();

            return services;
        }

        /// <summary>
        /// Register authentication services (mirrors mobile authentication architecture)
        /// </summary>
        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthenticationProvider, TelegramAuthProvider>();
            services.AddScoped<IAuthenticationProvider, ManualAuthProvider>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<ITelegramAuthJsInvoker, TelegramAuthJsInvoker>();


            // Token storage (LocalStorage equivalent of mobile secure storage)
            services.AddScoped<ITokenStorage, LocalStorageTokenStorage>();

            // Authentication state provider
            //services.AddScoped<ToxiqAuthenticationStateProvider>();
            //services.AddScoped<AuthenticationStateProvider>(provider =>
            //    provider.GetService<ToxiqAuthenticationStateProvider>()!);

            return services;
        }

        /// <summary>
        /// Register API services (mirrors mobile API service architecture)
        /// </summary>
        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {
            // Core API service (mirrors mobile HttpHandler)
            //services.AddScoped<IApiService, OptimizedApiService>();

            // Individual API services (mirrors mobile service breakdown)
            services.AddScoped<IPostService, PostServiceImpl>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ICommentService, CommentService>();
            //services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IColorService, ColorServiceImpl>();
            //services.AddScoped<IMediaService, MediaService>();

            // API response cache (mirrors mobile MonkeyCache)
            //already added in Program.cs
            //services.AddMemoryCache();
            //services.AddScoped<IApiCacheService, ApiCacheService>();

            return services;
        }

        /// <summary>
        /// Register feed services (mirrors mobile feed architecture)
        /// </summary>
        public static IServiceCollection AddFeedServices(this IServiceCollection services)
        {
            // Core feed service (mirrors mobile PostService feed methods)
            services.AddScoped<IFeedService, FeedService>();

            // Feed cache management (mirrors mobile memory service)
            //services.AddScoped<IFeedCacheService, FeedCacheService>();

            // Post interaction service (mirrors mobile post interaction handling)
            //services.AddScoped<IPostInteractionService, PostInteractionService>();

            return services;
        }

        /// <summary>
        /// Register UI component services
        /// </summary>
        public static IServiceCollection AddUIServices(this IServiceCollection services)
        {
            // Component state management
            services.AddScoped<IComponentStateService, ComponentStateService>();

            // Navigation service (enhanced navigation beyond basic NavigationManager)
            //services.AddScoped<INavigationService, NavigationService>();

            // Toast/notification service (for user feedback)
            services.AddScoped<IToastService, ToastService>();

            // Modal service (for popups and overlays)
            //services.AddScoped<IModalService, ModalService>();

            // Device detection service (for responsive behavior)
            //services.AddScoped<IDeviceService, DeviceService>();

            return services;
        }

        /// <summary>
        /// Register performance optimization services
        /// </summary>
        public static IServiceCollection AddPerformanceServices(this IServiceCollection services)
        {
            // Virtual scrolling service
            //services.AddScoped<IVirtualScrollService, VirtualScrollService>();

            // Image optimization service
            //services.AddScoped<IImageOptimizationService, ImageOptimizationService>();

            // Performance monitoring
            //services.AddScoped<IPerformanceMonitorService, PerformanceMonitorService>();

            // Memory management
            //services.AddSingleton<IMemoryManagementService, MemoryManagementService>();

            return services;
        }
    }
}
