using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Caching;
using Toxiq.WebApp.Client.Services.SignalR;

namespace Toxiq.WebApp.Client.Services.Authentication
{
    public interface IAuthenticationService
    {
        ValueTask<bool> IsAuthenticatedAsync();
        bool? IsAuthenticated();
        ValueTask<AuthenticationResult> LoginAsync(string inviteCode);
        ValueTask<AuthenticationResult> TryAutoLoginAsync();
        ValueTask LogoutAsync();
        ValueTask<UserProfile> GetCurrentUserAsync();
        Task<string?> GetTokenAsync();

        event EventHandler<AuthenticationStateChangedEventArgs> AuthenticationStateChanged;
    }

    public class AuthenticationStateChangedEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; set; }
        public UserProfile User { get; set; }
    }

    // Toxiq.WebApp.Client/Services/Authentication/AuthenticationService.cs
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ISignalRService _signalRService;

        private readonly IEnumerable<IAuthenticationProvider> _providers;
        private readonly ITokenStorage _tokenStorage;
        private readonly ICacheService _cache;
        private readonly IApiService _apiService;
        private readonly ILogger<AuthenticationService> _logger;

        private UserProfile _currentUser;
        private bool? _isAuthenticated;
        private bool _hasInitialized = false;

        public event EventHandler<AuthenticationStateChangedEventArgs> AuthenticationStateChanged;

        public AuthenticationService(
            IEnumerable<IAuthenticationProvider> providers,
            ITokenStorage tokenStorage,
            ICacheService cache,
            IApiService apiService,
            ILogger<AuthenticationService> logger, ISignalRService signalRService)
        {
            _providers = providers;
            _tokenStorage = tokenStorage;
            _cache = cache;
            _apiService = apiService;
            _logger = logger;
            _signalRService = signalRService;
        }

        public async ValueTask<bool> IsAuthenticatedAsync()
        {
            // Initialize on first call
            if (!_hasInitialized)
            {
                await InitializeAsync();
            }

            return _isAuthenticated ?? false;
        }

        public bool? IsAuthenticated()
        {
            return _isAuthenticated;
        }

        private async Task StartSignalRConnectionAsync()
        {
            try
            {
                var token = await _tokenStorage.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    await _signalRService.StartAsync(token);
                    _logger.LogInformation("SignalR connection started after authentication");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start SignalR connection after authentication");
                // Don't fail authentication if SignalR fails
            }
        }

        private async ValueTask InitializeAsync()
        {
            if (_hasInitialized) return;

            try
            {
                _logger.LogDebug("Initializing authentication service...");

                var token = await _tokenStorage.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogDebug("No stored token found");
                    _isAuthenticated = false;
                    _hasInitialized = true;
                    return;
                }

                _logger.LogDebug("Found stored token, validating...");

                // Try to validate the token with available providers
                foreach (var provider in _providers.Where(p => p.IsAvailable))
                {
                    try
                    {
                        var isValid = await provider.ValidateTokenAsync(token);
                        if (isValid)
                        {
                            _logger.LogDebug("Token validated successfully with provider {Provider}", provider.ProviderName);
                            _isAuthenticated = true;

                            // Try to load user profile
                            await LoadCurrentUserAsync();

                            // Notify that we're authenticated
                            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
                            {
                                IsAuthenticated = true,
                                User = _currentUser
                            });

                            try
                            {
                                await _signalRService.StartAsync(token);
                                _logger.LogInformation("SignalR connection started after login");
                            }
                            catch (Exception signalrEx)
                            {
                                _logger.LogWarning(signalrEx, "Failed to start SignalR connection after login");
                                // Don't fail login if SignalR fails
                            }

                            _hasInitialized = true;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Provider {Provider} failed to validate token", provider.ProviderName);
                    }
                }

                // If we get here, token is invalid
                _logger.LogWarning("Stored token is invalid, removing it");
                await _tokenStorage.RemoveTokenAsync();
                _isAuthenticated = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication initialization");
                _isAuthenticated = false;
            }
            finally
            {
                _hasInitialized = true;
            }
        }

        private async ValueTask LoadCurrentUserAsync()
        {
            try
            {
                _currentUser = await _cache.GetOrSetAsync("current_user", async () =>
                {
                    return await _apiService.UserService.GetMe();
                }, TimeSpan.FromMinutes(30));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load current user profile");
                _currentUser = null;
            }
        }

        public async ValueTask<AuthenticationResult> TryAutoLoginAsync()
        {
            // Initialize if not already done
            if (!_hasInitialized)
            {
                await InitializeAsync();
            }

            // Check if already authenticated
            if (_isAuthenticated == true)
            {
                var user = await GetCurrentUserAsync();
                return new AuthenticationResult(true, UserProfile: user);
            }



            // Try providers that support auto-login
            foreach (var provider in _providers.Where(p => p.IsAvailable))
            {
                try
                {
                    if (await provider.CanAutoLoginAsync())
                    {
                        var result = await provider.LoginAsync(new LoginRequest("auto", ""));
                        if (result.IsSuccess)
                        {
                            await OnAuthenticationSucceeded(result);
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-login failed for provider {Provider}", provider.ProviderName);
                }
            }

            return new AuthenticationResult(false, ErrorMessage: "Auto-login not available");
        }

        public async ValueTask<AuthenticationResult> LoginAsync(string inviteCode)
        {
            var request = new LoginRequest("", inviteCode);

            var manualProvider = _providers.FirstOrDefault(p => p.ProviderName == "Manual");
            if (manualProvider?.IsAvailable == true)
            {
                var result = await manualProvider.LoginAsync(request);
                if (result.IsSuccess)
                {
                    await OnAuthenticationSucceeded(result);
                }
                return result;
            }

            return new AuthenticationResult(false, ErrorMessage: "No authentication provider available");
        }

        public async ValueTask LogoutAsync()
        {
            // Notify all providers
            foreach (var provider in _providers.Where(p => p.IsAvailable))
            {
                try
                {
                    await provider.LogoutAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider {Provider} logout failed", provider.ProviderName);
                }
            }

            try
            {
                await _signalRService.StopAsync();
                _logger.LogInformation("SignalR connection stopped before logout");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping SignalR connection during logout");
            }

            // Clear local state
            await _tokenStorage.RemoveTokenAsync();
            await _cache.RemovePatternAsync("user_*");
            await _cache.RemovePatternAsync("api_*");

            _currentUser = null;
            _isAuthenticated = false;
            _hasInitialized = true; // Keep initialized but not authenticated

            // Notify state change
            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = false,
                User = null
            });

            _logger.LogInformation("User logged out successfully");
        }

        public async ValueTask<UserProfile> GetCurrentUserAsync()
        {
            if (!_hasInitialized)
            {
                await InitializeAsync();
            }

            if (_currentUser != null)
                return _currentUser;

            if (!(_isAuthenticated ?? false))
                return null;

            await LoadCurrentUserAsync();
            return _currentUser;
        }

        private async ValueTask OnAuthenticationSucceeded(AuthenticationResult result)
        {
            _currentUser = result.UserProfile;
            _isAuthenticated = true;
            _hasInitialized = true;

            // Cache user profile
            if (_currentUser != null)
            {
                await _cache.SetAsync("current_user", _currentUser, TimeSpan.FromHours(1));
            }

            // FIXED: Start SignalR connection after successful authentication
            await StartSignalRConnectionAsync();

            // Notify state change
            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = true,
                User = _currentUser
            });

            _logger.LogInformation("Authentication succeeded for user {UserName}", _currentUser?.UserName);
        }
        public async Task<string?> GetTokenAsync()
        {
            var token = await _tokenStorage.GetTokenAsync();
            return token;
        }
    }
}
