// Toxiq.WebApp.Client/Services/Notifications/SignalRService.cs
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Api;
using Toxiq.WebApp.Client.Services.Authentication;

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
        Task StartAsync();

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
        private readonly IAuthenticationService _authService;
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private HubConnection? _hubConnection;
        private readonly ILogger<SignalRService> _logger;

        public event EventHandler<NotificationDto>? NotificationReceived;
        public event EventHandler<bool>? ConnectionStateChanged;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SignalRService(
            IAuthenticationService authService,
            INotificationService notificationService,
            IConfiguration configuration,
            ILogger<SignalRService> logger)
        {
            _authService = authService;
            _notificationService = notificationService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Start SignalR connection to notification hub
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                if (_hubConnection != null && IsConnected)
                {
                    _logger.LogInformation("SignalR already connected");
                    return;
                }

                // Get API base URL from configuration
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7145";
                var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/notification";
                var token = await _authService.GetTokenAsync();

                // Create hub connection
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        // Add authentication token if available
                        if (!string.IsNullOrEmpty(token))
                        {
                            options.AccessTokenProvider = () => Task.FromResult(token);
                        }

                        // Configure transport
                        options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                           Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                    })
                    .WithAutomaticReconnect(new RetryPolicy())
                    .Build();

                // Setup event handlers
                SetupEventHandlers();

                // Start connection
                await _hubConnection.StartAsync();

                _logger.LogInformation($"SignalR connected to: {hubUrl}");
                ConnectionStateChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting SignalR connection");
                ConnectionStateChanged?.Invoke(this, false);
                throw;
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

            // Handle incoming notifications
            _hubConnection.On<string>("ReceiveNotification", async (notificationJson) =>
            {
                try
                {
                    var notification = JsonSerializer.Deserialize<NotificationDto>(notificationJson);
                    if (notification != null)
                    {
                        _logger.LogInformation($"Received notification: {notification.Type} - {notification.Text}");

                        // Add to notification service (which handles caching and unread count)
                        await _notificationService.AddNewNotification(notification);

                        // Notify components
                        NotificationReceived?.Invoke(this, notification);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing received notification");
                }
            });

            // Handle connection events
            _hubConnection.Closed += async (error) =>
            {
                _logger.LogWarning($"SignalR connection closed: {error?.Message}");
                ConnectionStateChanged?.Invoke(this, false);

                // Attempt to reconnect after delay
                await Task.Delay(5000);
                try
                {
                    await StartAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reconnecting SignalR");
                }
            };

            _hubConnection.Reconnecting += (error) =>
            {
                _logger.LogInformation($"SignalR reconnecting: {error?.Message}");
                ConnectionStateChanged?.Invoke(this, false);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation($"SignalR reconnected: {connectionId}");
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
            // Exponential backoff with jitter
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryContext.PreviousRetryCount));

            // Cap at 30 seconds
            if (delay > TimeSpan.FromSeconds(30))
            {
                delay = TimeSpan.FromSeconds(30);
            }

            // Add jitter (±25%)
            var jitter = new Random().NextDouble() * 0.5 + 0.75; // 0.75 to 1.25
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * jitter);

            // Stop retrying after 10 attempts
            return retryContext.PreviousRetryCount < 10 ? delay : null;
        }
    }
}