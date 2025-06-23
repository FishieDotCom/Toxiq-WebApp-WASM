using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Caching;

namespace Toxiq.WebApp.Client.Services.Authentication
{
    public interface IAuthenticationService
    {
        ValueTask<bool> IsAuthenticatedAsync();
        ValueTask<AuthenticationResult> LoginAsync(string inviteCode);
        ValueTask<AuthenticationResult> TryAutoLoginAsync();
        ValueTask LogoutAsync();
        ValueTask<UserProfile> GetCurrentUserAsync();
        event EventHandler<AuthenticationStateChangedEventArgs> AuthenticationStateChanged;
    }

    public class AuthenticationStateChangedEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; set; }
        public UserProfile User { get; set; }
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IEnumerable<IAuthenticationProvider> _providers;
        private readonly ITokenStorage _tokenStorage;
        private readonly ICacheService _cache;
        private readonly ILogger<AuthenticationService> _logger;

        private UserProfile _currentUser;
        private bool? _isAuthenticated;

        public event EventHandler<AuthenticationStateChangedEventArgs> AuthenticationStateChanged;

        public AuthenticationService(
            IEnumerable<IAuthenticationProvider> providers,
            ITokenStorage tokenStorage,
            ICacheService cache,
            ILogger<AuthenticationService> logger)
        {
            _providers = providers;
            _tokenStorage = tokenStorage;
            _cache = cache;
            _logger = logger;
        }

        public async ValueTask<bool> IsAuthenticatedAsync()
        {
            if (_isAuthenticated.HasValue)
                return _isAuthenticated.Value;

            var token = await _tokenStorage.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _isAuthenticated = false;
                return false;
            }

            // Validate token with available providers
            foreach (var provider in _providers.Where(p => p.IsAvailable))
            {
                try
                {
                    var isValid = await provider.ValidateTokenAsync(token);
                    if (isValid)
                    {
                        _isAuthenticated = true;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider {Provider} failed to validate token", provider.ProviderName);
                }
            }

            _isAuthenticated = false;
            await _tokenStorage.RemoveTokenAsync(); // Remove invalid token
            return false;
        }

        public async ValueTask<AuthenticationResult> LoginAsync(string inviteCode)
        {
            var request = new LoginRequest("manual", inviteCode);

            // Try manual provider first
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

        public async ValueTask<AuthenticationResult> TryAutoLoginAsync()
        {
            // Check if already authenticated
            if (await IsAuthenticatedAsync())
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

            // Clear local state
            await _tokenStorage.RemoveTokenAsync();
            await _cache.RemovePatternAsync("user_*");
            await _cache.RemovePatternAsync("api_*");

            _currentUser = null;
            _isAuthenticated = false;

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
            if (_currentUser != null)
                return _currentUser;

            if (!await IsAuthenticatedAsync())
                return null;

            try
            {
                _currentUser = await _cache.GetOrSetAsync("current_user", async () =>
                {
                    // This would need to be implemented in your API service
                    // For now, return a basic profile
                    return new UserProfile { UserName = "User", Name = "Current User" };
                }, TimeSpan.FromMinutes(30));

                return _currentUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current user");
                return null;
            }
        }

        private async ValueTask OnAuthenticationSucceeded(AuthenticationResult result)
        {
            _currentUser = result.UserProfile;
            _isAuthenticated = true;

            // Cache user profile
            if (_currentUser != null)
            {
                await _cache.SetAsync("current_user", _currentUser, TimeSpan.FromHours(1));
            }

            // Notify state change
            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = true,
                User = _currentUser
            });

            _logger.LogInformation("Authentication succeeded for user {UserName}", _currentUser?.UserName);
        }
    }
}
