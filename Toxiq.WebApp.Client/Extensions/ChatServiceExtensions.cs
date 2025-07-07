// Toxiq.WebApp.Client/Extensions/ChatServiceExtensions.cs
// Service registration extensions for chat functionality

using Toxiq.WebApp.Client.Services.Chat;

namespace Toxiq.WebApp.Client.Extensions
{
    /// <summary>
    /// Extension methods for registering chat services
    /// Follows mobile app's service architecture patterns
    /// </summary>
    public static class ChatServiceExtensions
    {
        /// <summary>
        /// Add chat services to the DI container
        /// Mirrors mobile app's service registration patterns
        /// </summary>
        public static IServiceCollection AddChatServices(this IServiceCollection services)
        {
            // Register chat service as scoped (matches mobile app lifecycle)
            services.AddScoped<IChatService, ChatService>();

            // Add any chat-specific background services if needed
            // services.AddSingleton<IChatNotificationService, ChatNotificationService>();

            return services;
        }

        /// <summary>
        /// Configure chat-specific options
        /// </summary>
        public static IServiceCollection ConfigureChatOptions(this IServiceCollection services, Action<ChatOptions> configure)
        {
            services.Configure(configure);
            return services;
        }
    }

    /// <summary>
    /// Chat configuration options
    /// </summary>
    public class ChatOptions
    {
        /// <summary>
        /// Default page size for message pagination
        /// </summary>
        public int DefaultPageSize { get; set; } = 20;

        /// <summary>
        /// Maximum message length
        /// </summary>
        public int MaxMessageLength { get; set; } = 500;

        /// <summary>
        /// Cache expiration time for conversations
        /// </summary>
        public TimeSpan ConversationCacheExpiration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Cache expiration time for messages
        /// </summary>
        public TimeSpan MessageCacheExpiration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Enable real-time notifications
        /// </summary>
        public bool EnableRealTimeNotifications { get; set; } = true;

        /// <summary>
        /// Enable browser notifications
        /// </summary>
        public bool EnableBrowserNotifications { get; set; } = true;

        /// <summary>
        /// Auto-scroll to new messages
        /// </summary>
        public bool AutoScrollToNewMessages { get; set; } = true;

        /// <summary>
        /// Show typing indicators
        /// </summary>
        public bool ShowTypingIndicators { get; set; } = true;

        /// <summary>
        /// Message edit time limit in minutes
        /// </summary>
        public int MessageEditTimeLimitMinutes { get; set; } = 15;

        /// <summary>
        /// Enable message reactions
        /// </summary>
        public bool EnableMessageReactions { get; set; } = true;

        /// <summary>
        /// Enable file attachments
        /// </summary>
        public bool EnableFileAttachments { get; set; } = true;

        /// <summary>
        /// Maximum file size for attachments in bytes
        /// </summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Allowed file types for attachments
        /// </summary>
        public string[] AllowedFileTypes { get; set; } =
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "video/mp4", "video/webm",
            "audio/mp3", "audio/wav", "audio/ogg",
            "application/pdf",
            "text/plain"
        };
    }
}
