using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Toxiq.Mobile.Dto;

namespace Toxiq.WebApp.Client.Services.Notifications
{
    public interface ISignalRService
    {
        Task StartAsync(string token);
        Task StopAsync();
        bool IsConnected { get; }
        event EventHandler<NotificationDto>? NotificationReceived;
        event EventHandler<bool>? ConnectionStateChanged;
    }

    public class SignalRService : ISignalRService, IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SignalRService> _logger;

        // NOTE: Can't inject INotificationService here since SignalR is singleton 
        // and NotificationService is scoped. We'll use events instead.

        private HubConnection? _hubConnection;
        private string? _currentToken;
        private readonly object _connectionLock = new object();

        public event EventHandler<NotificationDto>? NotificationReceived;
        public event EventHandler<bool>? ConnectionStateChanged;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SignalRService(
            IConfiguration configuration,
            ILogger<SignalRService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(string token)
        {
            // Thread-safe connection management
            lock (_connectionLock)
            {
                // Check if already connected with same token
                if (_hubConnection != null && IsConnected && _currentToken == token)
                {
                    _logger.LogInformation("SignalR already connected with current token");
                    return;
                }
            }

            try
            {
                // Stop existing connection if any
                if (_hubConnection != null)
                {
                    await StopInternalAsync();
                }

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("No authentication token available for SignalR connection");
                    ConnectionStateChanged?.Invoke(this, false);
                    return;
                }

                _currentToken = token;

                // FIXED: Build correct hub URL
                var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://toxiq.xyz/api/";
                var baseUrl = apiBaseUrl.TrimEnd('/').Replace("/api", "");
                var hubUrl = $"{baseUrl}/hubs/notification";

                _logger.LogInformation($"Connecting to SignalR hub: {hubUrl}");

                // FIXED: Create connection with proper configuration for singleton
                lock (_connectionLock)
                {
                    _hubConnection = new HubConnectionBuilder()
                        .WithUrl(hubUrl, options =>
                        {
                            // Primary authentication method
                            options.AccessTokenProvider = () => Task.FromResult(token)!;

                            // Backup authentication header
                            options.Headers.Add("Authorization", $"Bearer {token}");

                            // Transport configuration for WebAssembly
                            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                                               Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;

                            // FIXED: WebAssembly specific settings
                            options.SkipNegotiation = false;  // Allow negotiation for better compatibility
                            options.UseStatefulReconnect = false; // Disable for WebAssembly

                            // Timeout settings
                            options.CloseTimeout = TimeSpan.FromSeconds(30);
                        })
                        .WithAutomaticReconnect(new CustomRetryPolicy())
                        .ConfigureLogging(logging =>
                        {
                            logging.SetMinimumLevel(LogLevel.Debug);
                            //logging.AddConsole();
                        })
                        .Build();
                }

                // Setup event handlers BEFORE starting connection
                SetupEventHandlers();

                // Start connection
                _logger.LogInformation("Starting SignalR connection...");
                await _hubConnection.StartAsync();

                _logger.LogInformation($"SignalR connected successfully!");
                _logger.LogInformation($"Connection ID: {_hubConnection.ConnectionId}");

                ConnectionStateChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting SignalR connection");
                ConnectionStateChanged?.Invoke(this, false);

                // Clean up failed connection
                lock (_connectionLock)
                {
                    if (_hubConnection != null)
                    {
                        try
                        {
                            _hubConnection.DisposeAsync().GetAwaiter().GetResult();
                        }
                        catch { }
                        _hubConnection = null;
                    }
                }

                throw; // Re-throw for caller to handle
            }
        }

        private void SetupEventHandlers()
        {
            if (_hubConnection == null) return;

            // FIXED: Handle notifications with proper error handling
            _hubConnection.On<string>("ReceiveNotification", async (notificationJson) =>
            {
                try
                {
                    _logger.LogInformation($"Raw notification received: {notificationJson}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var notification = JsonSerializer.Deserialize<NotificationDto>(notificationJson);

                    if (notification != null)
                    {
                        _logger.LogInformation($"Parsed notification: {notification.Type} - {notification.Text}");

                        // FIXED: Since we're singleton, just fire the event
                        // Scoped services will handle their own logic
                        NotificationReceived?.Invoke(this, notification);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize notification JSON");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing notification: {notificationJson}");
                }
            });

            // FIXED: Also handle direct NotificationDto objects (in case server sends objects)
            _hubConnection.On<NotificationDto>("ReceiveNotification", async (notification) =>
            {
                try
                {
                    _logger.LogInformation($"Direct notification received: {notification.Type} - {notification.Text}");

                    // Fire event for scoped services to handle
                    NotificationReceived?.Invoke(this, notification);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing direct notification");
                }
            });

            // Connection status handlers
            _hubConnection.On<string>("Connected", (message) =>
            {
                _logger.LogInformation($"Hub Connected message: {message}");
            });

            // Connection status handlers
            _hubConnection.On<string>("Ping", (message) =>
            {
                NotificationReceived?.Invoke(this, new NotificationDto { Caption = "ping", Text = "pong" });
                _logger.LogInformation($"Pong: {message}");
            });


            // Connection event handlers
            _hubConnection.Closed += async (error) =>
            {
                _logger.LogWarning($"SignalR connection closed: {error?.Message}");
                ConnectionStateChanged?.Invoke(this, false);
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

        public async Task StopAsync()
        {
            await StopInternalAsync();
            ConnectionStateChanged?.Invoke(this, false);
        }

        private async Task StopInternalAsync()
        {
            HubConnection? connectionToStop = null;

            lock (_connectionLock)
            {
                if (_hubConnection != null)
                {
                    connectionToStop = _hubConnection;
                    _hubConnection = null;
                    _currentToken = null;
                }
            }

            if (connectionToStop != null)
            {
                try
                {
                    await connectionToStop.StopAsync();
                    await connectionToStop.DisposeAsync();
                    _logger.LogInformation("SignalR connection stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping SignalR connection");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopInternalAsync();
        }
    }

    // FIXED: Custom retry policy for WebAssembly
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
}