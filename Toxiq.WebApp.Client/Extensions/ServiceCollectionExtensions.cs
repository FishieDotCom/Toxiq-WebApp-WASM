// Toxiq.WebApp.Client/Extensions/ServiceCollectionExtensions.cs
using Toxiq.WebApp.Client.Services.Api;
using Toxiq.WebApp.Client.Services.Feed;

namespace Toxiq.WebApp.Client.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFeedServices(this IServiceCollection services)
        {
            // Register feed services
            services.AddScoped<IFeedService, FeedService>();

            return services;
        }

        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {
            // Register API services - mirrors mobile app service architecture
            services.AddScoped<ICommentService, CommentService>();

            // Add other API services as needed
            // services.AddScoped<IPostService, PostService>();
            // services.AddScoped<IUserService, UserService>();
            // services.AddScoped<INotificationService, NotificationService>();

            return services;
        }

        public static IServiceCollection AddToxiqServices(this IServiceCollection services, string baseUrl)
        {
            // Configure HttpClient for API calls
            services.AddHttpClient("ToxiqApi", client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("User-Agent", "Toxiq-WebApp/1.0");
            });

            // Add authentication services (from your existing implementation)
            //services.AddAuthenticationServices();

            // Add API services (from your existing implementation)
            //services.AddApiServices();

            // Add feed services
            services.AddFeedServices();
            return services;
        }


    }
}
