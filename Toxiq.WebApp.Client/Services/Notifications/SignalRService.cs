// Toxiq.WebApp.Client/Services/Notifications/SignalRService.cs
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Api;

namespace Toxiq.WebApp.Client.Services.Notifications
{
    /// <summary>
    /// SignalR service interface for real-time notifications
    /// </summary>
    public interface ISignalRService
    {
        /// <summary>
        /// Start SignalR connection
        /// </summary>
        Task StartAsync(string token);

        /// <summary>
        /// Stop SignalR connection
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Check if connection is active
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Event fired when new notification is received
        /// </summary>
        event EventHandler<NotificationDto>? NotificationReceived;

        /// <summary>
        /// Event fired when connection state changes
        /// </summary>
        event EventHandler<bool>? ConnectionStateChanged;
    }

    /// <summary>
    /// SignalR service implementation for real-time notification delivery
    /// Connects to /hubs/notification endpoint
    /// </summary>
    public class SignalRService : ISignalRService, IAsyncDisposable
    {
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private HubConnection? _hubConnection;
        private readonly ILogger<SignalRService> _logger;

        public event EventHandler<NotificationDto>? NotificationReceived;
        public event EventHandler<bool>? ConnectionStateChanged;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SignalRService(
            INotificationService notificationService,
            IConfiguration configuration,
            ILogger<SignalRService> logger)
        {

            _notificationService = notificationService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Start SignalR connection to notification hub
        /// </summary>
        public async Task StartAsync(string token)
        {
            try
            {
                if (_hubConnection != null && IsConnected)
                {
                    _logger.LogInformation("SignalR already connected");
                    return;
                }

                // FIXED: Build correct hub URL - remove /api/ for hub endpoints
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://toxiq.xyz/api/";

                // Remove /api/ suffix and build hub URL correctly
                var baseUrl = apiBaseUrl.Replace("/api/", "").TrimEnd('/');
                var hubUrl = $"{baseUrl}/hubs/notification";

                _logger.LogInformation($"Attempting to connect to SignalR hub: {hubUrl}");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("No authentication token available for SignalR connection");
                    return;
                }

                _logger.LogInformation("Token retrieved successfully for SignalR connection");

                // Create hub connection
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        // Add authentication token
                        options.AccessTokenProvider = () => Task.FromResult(token);

                        // FIXED: Configure transport and additional options
                        options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                           Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;

                        // Add headers that match your test client
                        options.Headers.Add("Authorization", $"Bearer {token}");
                        options.UseStatefulReconnect = true;
                        // Skip negotiation for better compatibility
                        options.SkipNegotiation = false;
                    })
                    .WithAutomaticReconnect(new RetryPolicy())
                    .ConfigureLogging(logging =>
                    {
                        //logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Debug);
                    })
                    .Build();

                // Setup event handlers
                SetupEventHandlers();

                // Start connection
                _logger.LogInformation("Starting SignalR connection...");
                await _hubConnection.StartAsync();

                _logger.LogInformation($"SignalR connected successfully to: {hubUrl}");
                _logger.LogInformation($"Connection ID: {_hubConnection.ConnectionId}");
                ConnectionStateChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting SignalR connection");
                ConnectionStateChanged?.Invoke(this, false);

                // Don't rethrow - let the application continue without real-time notifications
                // throw;
            }
        }

        /// <summary>
        /// Stop SignalR connection
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_hubConnection != null)
                {
                    await _hubConnection.StopAsync();
                    _logger.LogInformation("SignalR connection stopped");
                    ConnectionStateChanged?.Invoke(this, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping SignalR connection");
            }
        }

        /// <summary>
        /// Setup SignalR event handlers
        /// </summary>
        private void SetupEventHandlers()
        {
            if (_hubConnection == null) return;

            // FIXED: Handle incoming notifications - match the server method signature
            _hubConnection.On<string>("ReceiveNotification", async (notificationJson) =>
            {
                try
                {
                    _logger.LogInformation($"Raw notification received: {notificationJson}");

                    var notification = JsonSerializer.Deserialize<NotificationDto>(notificationJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (notification != null)
                    {
                        _logger.LogInformation($"Parsed notification: {notification.Type} - {notification.Text}");

                        // Add to notification service (which handles caching and unread count)
                        await _notificationService.AddNewNotification(notification);

                        // Notify components
                        NotificationReceived?.Invoke(this, notification);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize notification JSON");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing received notification: {notificationJson}");
                }
            });

            // FIXED: Add connection status handlers that match your test client
            _hubConnection.On<string>("Connected", (message) =>
            {
                _logger.LogInformation($"Hub Connected message: {message}");
            });

            // Handle connection events
            _hubConnection.Closed += async (error) =>
            {
                _logger.LogWarning($"SignalR connection closed: {error?.Message}");
                ConnectionStateChanged?.Invoke(this, false);

                // FIXED: Don't auto-reconnect immediately - let the retry policy handle it
                // The WithAutomaticReconnect will handle this automatically
            };

            _hubConnection.Reconnecting += (error) =>
            {
                _logger.LogInformation($"SignalR reconnecting: {error?.Message}");
                ConnectionStateChanged?.Invoke(this, false);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation($"SignalR reconnected with ID: {connectionId}");
                ConnectionStateChanged?.Invoke(this, true);
                return Task.CompletedTask;
            };
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Custom retry policy for SignalR reconnection
    /// </summary>
    public class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            // FIXED: More conservative retry policy
            var delays = new[]
            {
                TimeSpan.FromSeconds(1),
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

            // Stop retrying after 6 attempts
            return null;
        }
    }
}