// Toxiq.WebApp.Client/Services/SignalR/ISignalRService.cs
// Updated SignalR service interface following Hub Gateway pattern

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.SignalR;


namespace Toxiq.WebApp.Client.Services.SignalR
{
    /// <summary>
    /// SignalR service interface following Hub Gateway pattern
    /// Manages connections to separate Chat and Notification hubs
    /// </summary>
    public interface ISignalRService
    {
        /// <summary>
        /// Connection states for both hubs
        /// </summary>
        bool IsChatConnected { get; }
        bool IsNotificationConnected { get; }

        /// <summary>
        /// Start connections to both hubs
        /// </summary>
        Task<bool> StartAsync(string token);

        /// <summary>
        /// Stop all hub connections
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Chat hub specific methods
        /// </summary>
        Task<bool> JoinConversationAsync(Guid conversationId);
        Task<bool> LeaveConversationAsync(Guid conversationId);
        Task<bool> SendMessageAsync(Guid conversationId, string message);
        Task<bool> SendTypingIndicatorAsync(Guid conversationId, bool isTyping);

        #region Chat Hub Events

        /// <summary>
        /// Fired when a new message is received via chat hub
        /// </summary>
        event EventHandler<Message>? MessageReceived;

        /// <summary>
        /// Fired when a message is updated via chat hub
        /// </summary>
        event EventHandler<Message>? MessageUpdated;

        /// <summary>
        /// Fired when a message is deleted via chat hub
        /// </summary>
        event EventHandler<Guid>? MessageDeleted;

        /// <summary>
        /// Fired when someone starts/stops typing
        /// </summary>
        event EventHandler<TypingIndicator>? TypingIndicatorReceived;

        /// <summary>
        /// Fired when conversation is updated
        /// </summary>
        event EventHandler<Conversation>? ConversationUpdated;

        /// <summary>
        /// Fired when user joins conversation
        /// </summary>
        event EventHandler<Guid>? JoinedConversation;

        #endregion

        #region Notification Hub Events

        /// <summary>
        /// Fired when a new notification is received via notification hub
        /// </summary>
        event EventHandler<NotificationDto>? NotificationReceived;

        /// <summary>
        /// Fired when post interactions change (upvotes, comments, etc.)
        /// </summary>
        event EventHandler<object>? PostInteractionReceived;

        #endregion

        #region Connection Events

        /// <summary>
        /// Fired when connection state changes
        /// </summary>
        event EventHandler<bool>? ConnectionStateChanged;

        #endregion
    }

    /// <summary>
    /// Typing indicator data structure
    /// </summary>
    public class TypingIndicator
    {
        public Guid ConversationId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool IsTyping { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

// Toxiq.WebApp.Client/Services/SignalR/SignalRService.cs
// Updated SignalR service implementation following Hub Gateway pattern

namespace Toxiq.WebApp.Client.Services.SignalR
{
    /// <summary>
    /// SignalR service implementation following Hub Gateway pattern
    /// Manages separate connections to Chat and Notification hubs
    /// </summary>
    public class SignalRService : ISignalRService, IAsyncDisposable
    {
        private readonly ILogger<SignalRService> _logger;
        private readonly IConfiguration _configuration;
        private readonly SignalROptions _options;

        // Separate hub connections following gateway pattern
        private HubConnection? _chatHubConnection;
        private HubConnection? _notificationHubConnection;

        private string? _currentToken;
        private readonly object _connectionLock = new object();
        private readonly HashSet<Guid> _joinedConversations = new();
        private bool _disposed = false;

        public bool IsChatConnected => _chatHubConnection?.State == HubConnectionState.Connected;
        public bool IsNotificationConnected => _notificationHubConnection?.State == HubConnectionState.Connected;

        #region Chat Hub Events

        public event EventHandler<Message>? MessageReceived;
        public event EventHandler<Message>? MessageUpdated;
        public event EventHandler<Guid>? MessageDeleted;
        public event EventHandler<TypingIndicator>? TypingIndicatorReceived;
        public event EventHandler<Conversation>? ConversationUpdated;
        public event EventHandler<Guid>? JoinedConversation;

        #endregion

        #region Notification Hub Events

        public event EventHandler<NotificationDto>? NotificationReceived;
        public event EventHandler<object>? PostInteractionReceived;

        #endregion

        #region Connection Events

        public event EventHandler<bool>? ConnectionStateChanged;

        #endregion

        public SignalRService(
            ILogger<SignalRService> logger,
            IConfiguration configuration,
            IOptions<SignalROptions> options)
        {
            _logger = logger;
            _configuration = configuration;
            _options = options.Value;
        }

        public async Task<bool> StartAsync(string token)
        {
            // Thread-safe connection management
            lock (_connectionLock)
            {
                // Check if already connected with same token
                if (_chatHubConnection != null && IsChatConnected &&
                    _notificationHubConnection != null && IsNotificationConnected &&
                    _currentToken == token)
                {
                    _logger.LogInformation("SignalR already connected to both hubs with current token");
                    return true;
                }
            }

            try
            {
                // Stop existing connections if any
                await StopInternalAsync();

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("No authentication token available for SignalR connection");
                    ConnectionStateChanged?.Invoke(this, false);
                    return false;
                }

                _currentToken = token;

                // Build hub URLs following the gateway pattern
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://toxiq.xyz/api/";
                var baseUrl = apiBaseUrl.TrimEnd('/').Replace("/api", "");

                var chatHubUrl = $"{baseUrl}/hubs/chat";
                var notificationHubUrl = $"{baseUrl}/hubs/notification";

                _logger.LogInformation("Connecting to Chat Hub: {ChatHubUrl}", chatHubUrl);
                _logger.LogInformation("Connecting to Notification Hub: {NotificationHubUrl}", notificationHubUrl);

                // Connect to Chat Hub
                var chatConnected = await ConnectToChatHub(chatHubUrl, token);

                // Connect to Notification Hub
                var notificationConnected = await ConnectToNotificationHub(notificationHubUrl, token);

                var overallSuccess = chatConnected && notificationConnected;
                ConnectionStateChanged?.Invoke(this, overallSuccess);

                if (overallSuccess)
                {
                    _logger.LogInformation("Successfully connected to both Chat and Notification hubs");
                }
                else
                {
                    _logger.LogWarning("Failed to connect to one or more hubs. Chat: {ChatConnected}, Notification: {NotificationConnected}",
                        chatConnected, notificationConnected);
                }

                return overallSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting SignalR connections");
                ConnectionStateChanged?.Invoke(this, false);
                return false;
            }
        }

        private async Task<bool> ConnectToChatHub(string hubUrl, string token)
        {
            try
            {
                lock (_connectionLock)
                {
                    _chatHubConnection = new HubConnectionBuilder()
                        .WithUrl(hubUrl, options =>
                        {
                            options.AccessTokenProvider = () => Task.FromResult(token)!;
                            options.Headers.Add("Authorization", $"Bearer {token}");

                            // Transport configuration for WebAssembly
                            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                               Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                            options.SkipNegotiation = false;
                            options.CloseTimeout = TimeSpan.FromSeconds(30);
                        })
                        .WithAutomaticReconnect(new CustomRetryPolicy())
                        .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug))
                        .Build();
                }

                // Setup chat hub event handlers BEFORE starting connection
                SetupChatHubEventHandlers();

                // Start chat hub connection
                await _chatHubConnection.StartAsync();

                _logger.LogInformation("Connected to Chat Hub successfully! Connection ID: {ConnectionId}",
                    _chatHubConnection.ConnectionId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to Chat Hub");

                // Clean up failed connection
                lock (_connectionLock)
                {
                    if (_chatHubConnection != null)
                    {
                        try
                        {
                            _chatHubConnection.DisposeAsync().GetAwaiter().GetResult();
                        }
                        catch { }
                        _chatHubConnection = null;
                    }
                }

                return false;
            }
        }

        private async Task<bool> ConnectToNotificationHub(string hubUrl, string token)
        {
            try
            {
                lock (_connectionLock)
                {
                    _notificationHubConnection = new HubConnectionBuilder()
                        .WithUrl(hubUrl, options =>
                        {
                            options.AccessTokenProvider = () => Task.FromResult(token)!;
                            options.Headers.Add("Authorization", $"Bearer {token}");

                            // Transport configuration for WebAssembly
                            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                               Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                            options.SkipNegotiation = false;
                            options.CloseTimeout = TimeSpan.FromSeconds(30);
                        })
                        .WithAutomaticReconnect(new CustomRetryPolicy())
                        .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug))
                        .Build();
                }

                // Setup notification hub event handlers BEFORE starting connection
                SetupNotificationHubEventHandlers();

                // Start notification hub connection
                await _notificationHubConnection.StartAsync();

                _logger.LogInformation("Connected to Notification Hub successfully! Connection ID: {ConnectionId}",
                    _notificationHubConnection.ConnectionId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to Notification Hub");

                // Clean up failed connection
                lock (_connectionLock)
                {
                    if (_notificationHubConnection != null)
                    {
                        try
                        {
                            _notificationHubConnection.DisposeAsync().GetAwaiter().GetResult();
                        }
                        catch { }
                        _notificationHubConnection = null;
                    }
                }

                return false;
            }
        }

        private void SetupChatHubEventHandlers()
        {
            if (_chatHubConnection == null) return;

            // Message received - follows the pattern from ChatHubClient.cs
            _chatHubConnection.On<Message>("ReceiveMessage", (message) =>
            {
                _logger.LogDebug("Received message {MessageId} in conversation", message.Id);
                MessageReceived?.Invoke(this, message);
            });

            // System message handler
            _chatHubConnection.On<string>("SystemMessage", (message) =>
            {
                _logger.LogDebug("Chat system message: {Message}", message);
            });

            // Chat connection status
            _chatHubConnection.On<string>("ChatConnected", (message) =>
            {
                _logger.LogInformation("Chat Hub connected: {Message}", message);
            });

            // Joined conversation confirmation - follows pattern from ChatHubClient.cs
            _chatHubConnection.On<Guid>("JoinedConversation", (conversationId) =>
            {
                _logger.LogDebug("Joined conversation: {ConversationId}", conversationId);
                _joinedConversations.Add(conversationId);
                JoinedConversation?.Invoke(this, conversationId);
            });

            // Message updated (edited)
            _chatHubConnection.On<Message>("MessageUpdated", (message) =>
            {
                _logger.LogDebug("Message updated {MessageId}", message.Id);
                MessageUpdated?.Invoke(this, message);
            });

            // Message deleted
            _chatHubConnection.On<Guid>("MessageDeleted", (messageId) =>
            {
                _logger.LogDebug("Message deleted {MessageId}", messageId);
                MessageDeleted?.Invoke(this, messageId);
            });

            // Typing indicator
            _chatHubConnection.On<TypingIndicator>("TypingIndicator", (typingIndicator) =>
            {
                _logger.LogDebug("Typing indicator from {UserId} in {ConversationId}: {IsTyping}",
                    typingIndicator.UserId, typingIndicator.ConversationId, typingIndicator.IsTyping);
                TypingIndicatorReceived?.Invoke(this, typingIndicator);
            });

            // Conversation updated
            _chatHubConnection.On<Conversation>("ConversationUpdated", (conversation) =>
            {
                _logger.LogDebug("Conversation updated {ConversationId}", conversation.Id);
                ConversationUpdated?.Invoke(this, conversation);
            });

            // Chat hub connection event handlers
            _chatHubConnection.Reconnecting += (error) =>
            {
                _logger.LogWarning("Chat Hub reconnecting: {Error}", error?.Message);
                return Task.CompletedTask;
            };

            _chatHubConnection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation("Chat Hub reconnected with ID: {ConnectionId}", connectionId);

                // Rejoin all conversations after reconnection
                foreach (var conversationId in _joinedConversations.ToList())
                {
                    await _chatHubConnection.InvokeAsync("JoinConversation", conversationId);
                }
            };

            _chatHubConnection.Closed += (error) =>
            {
                _logger.LogWarning("Chat Hub connection closed: {Error}", error?.Message);
                return Task.CompletedTask;
            };
        }

        private void SetupNotificationHubEventHandlers()
        {
            if (_notificationHubConnection == null) return;

            // Notification received - follows pattern from existing SignalRService.cs
            _notificationHubConnection.On<string>("ReceiveNotification", async (notificationJson) =>
            {
                try
                {
                    _logger.LogDebug("Raw notification received: {NotificationJson}", notificationJson);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var notification = JsonSerializer.Deserialize<NotificationDto>(notificationJson, options);
                    if (notification != null)
                    {
                        _logger.LogDebug("Parsed notification {NotificationId}: {Text}", notification.Id, notification.Text);
                        NotificationReceived?.Invoke(this, notification);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse notification JSON: {Json}", notificationJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notification: {Json}", notificationJson);
                }
            });

            // Connection status handlers - follows existing pattern
            _notificationHubConnection.On<string>("Connected", (message) =>
            {
                _logger.LogInformation("Notification Hub connected: {Message}", message);
            });

            _notificationHubConnection.On<string>("Ping", (message) =>
            {
                _logger.LogDebug("Notification Hub ping: {Message}", message);
            });

            // Post interaction received
            _notificationHubConnection.On<object>("PostInteractionReceived", (interaction) =>
            {
                _logger.LogDebug("Received post interaction");
                PostInteractionReceived?.Invoke(this, interaction);
            });

            // Notification hub connection event handlers
            _notificationHubConnection.Reconnecting += (error) =>
            {
                _logger.LogWarning("Notification Hub reconnecting: {Error}", error?.Message);
                return Task.CompletedTask;
            };

            _notificationHubConnection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation("Notification Hub reconnected with ID: {ConnectionId}", connectionId);
                return Task.CompletedTask;
            };

            _notificationHubConnection.Closed += (error) =>
            {
                _logger.LogWarning("Notification Hub connection closed: {Error}", error?.Message);
                return Task.CompletedTask;
            };
        }

        #region Chat Hub Methods

        public async Task<bool> JoinConversationAsync(Guid conversationId)
        {
            if (!IsChatConnected) return false;

            try
            {
                await _chatHubConnection!.InvokeAsync("JoinConversation", conversationId);
                _logger.LogDebug("Requested to join conversation {ConversationId}", conversationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining conversation {ConversationId}", conversationId);
                return false;
            }
        }

        public async Task<bool> LeaveConversationAsync(Guid conversationId)
        {
            if (!IsChatConnected) return false;

            try
            {
                await _chatHubConnection!.InvokeAsync("LeaveConversation", conversationId);
                _joinedConversations.Remove(conversationId);
                _logger.LogDebug("Left conversation {ConversationId}", conversationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving conversation {ConversationId}", conversationId);
                return false;
            }
        }

        public async Task<bool> SendMessageAsync(Guid conversationId, string message)
        {
            if (!IsChatConnected) return false;

            try
            {
                // Follow the pattern from ChatHubClient.cs
                var messageObj = new MessageDto
                {
                    Id = Guid.NewGuid(),
                    SenderID = Guid.NewGuid(), // This should be set to current user ID
                    RecipientID = conversationId, // Using conversation ID as recipient
                    Content = message,
                    Type = MessageType.Text,
                    Date = DateTime.UtcNow
                };

                await _chatHubConnection!.InvokeAsync("SendMessage", messageObj);
                _logger.LogDebug("Sent message to conversation {ConversationId}", conversationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to conversation {ConversationId}", conversationId);
                return false;
            }
        }

        public async Task<bool> SendTypingIndicatorAsync(Guid conversationId, bool isTyping)
        {
            if (!IsChatConnected) return false;

            try
            {
                await _chatHubConnection!.InvokeAsync("SendTypingIndicator", conversationId, isTyping);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending typing indicator for conversation {ConversationId}", conversationId);
                return false;
            }
        }

        #endregion

        public async Task StopAsync()
        {
            await StopInternalAsync();
            ConnectionStateChanged?.Invoke(this, false);
        }

        private async Task StopInternalAsync()
        {
            HubConnection? chatConnectionToStop = null;
            HubConnection? notificationConnectionToStop = null;

            lock (_connectionLock)
            {
                if (_chatHubConnection != null)
                {
                    chatConnectionToStop = _chatHubConnection;
                    _chatHubConnection = null;
                }

                if (_notificationHubConnection != null)
                {
                    notificationConnectionToStop = _notificationHubConnection;
                    _notificationHubConnection = null;
                }

                _currentToken = null;
                _joinedConversations.Clear();
            }

            // Stop chat hub connection
            if (chatConnectionToStop != null)
            {
                try
                {
                    await chatConnectionToStop.StopAsync();
                    await chatConnectionToStop.DisposeAsync();
                    _logger.LogInformation("Chat Hub connection stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping Chat Hub connection");
                }
            }

            // Stop notification hub connection
            if (notificationConnectionToStop != null)
            {
                try
                {
                    await notificationConnectionToStop.StopAsync();
                    await notificationConnectionToStop.DisposeAsync();
                    _logger.LogInformation("Notification Hub connection stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping Notification Hub connection");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _disposed = true;
            await StopInternalAsync();
        }
    }

    /// <summary>
    /// Custom retry policy for WebAssembly - from existing implementation
    /// </summary>
    public class CustomRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            // Conservative retry delays for WebAssembly
            var delays = new[]
            {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)
            };

            if (retryContext.PreviousRetryCount < delays.Length)
            {
                return delays[retryContext.PreviousRetryCount];
            }

            return null; // Stop retrying after 5 attempts
        }
    }

    /// <summary>
    /// SignalR configuration options
    /// </summary>
    public class SignalROptions
    {
        public string ChatHubUrl { get; set; } = string.Empty;
        public string NotificationHubUrl { get; set; } = string.Empty;
        public bool AutoConnect { get; set; } = true;
        public int ReconnectAttempts { get; set; } = 10;
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(3);
        public bool EnableChatFeatures { get; set; } = true;
        public bool EnableNotificationFeatures { get; set; } = true;
        public bool EnableTypingIndicators { get; set; } = true;
    }
}


namespace Toxiq.WebApp.Client.Extensions
{
    /// <summary>
    /// Extension methods for registering SignalR services with Hub Gateway pattern
    /// </summary>
    public static class SignalRServiceExtensions
    {
        /// <summary>
        /// Add SignalR services with Hub Gateway pattern support
        /// </summary>
        public static IServiceCollection AddSignalRHubGateway(this IServiceCollection services, string baseUrl)
        {
            baseUrl = "https://toxiq.xyz";
            var chatHubUrl = $"{baseUrl.TrimEnd('/')}/hubs/chat";
            var notificationHubUrl = $"{baseUrl.TrimEnd('/')}/hubs/notification";

            // Configure SignalR options
            services.Configure<SignalROptions>(options =>
            {
                options.ChatHubUrl = chatHubUrl;
                options.NotificationHubUrl = notificationHubUrl;
                options.AutoConnect = true;
                options.ReconnectAttempts = 10;
                options.ReconnectInterval = TimeSpan.FromSeconds(3);
                options.EnableChatFeatures = true;
                options.EnableNotificationFeatures = true;
                options.EnableTypingIndicators = true;
            });

            // Register SignalR service as singleton (maintains connection state)
            services.AddSingleton<ISignalRService, SignalRService>();

            return services;
        }

        /// <summary>
        /// Add SignalR services with custom Hub Gateway configuration
        /// </summary>
        public static IServiceCollection AddSignalRHubGateway(this IServiceCollection services, Action<SignalROptions> configure)
        {
            services.Configure(configure);
            services.AddSingleton<ISignalRService, SignalRService>();
            return services;
        }
    }
}

