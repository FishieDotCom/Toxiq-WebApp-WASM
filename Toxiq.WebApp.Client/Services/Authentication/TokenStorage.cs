using Blazored.LocalStorage;

namespace Toxiq.WebApp.Client.Services.Authentication
{
    public interface ITokenStorage
    {
        ValueTask<string> GetTokenAsync();
        ValueTask SetTokenAsync(string token);
        ValueTask RemoveTokenAsync();
        ValueTask<bool> HasTokenAsync();
    }

    public class LocalStorageTokenStorage : ITokenStorage
    {
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<LocalStorageTokenStorage> _logger;
        private const string TokenKey = "auth_token";

        public LocalStorageTokenStorage(ILocalStorageService localStorage, ILogger<LocalStorageTokenStorage> logger)
        {
            _localStorage = localStorage;
            _logger = logger;
            _logger.LogDebug("LocalStorageTokenStorage initialized");
        }

        public async ValueTask<string> GetTokenAsync()
        {
            try
            {
                _logger.LogDebug("Attempting to retrieve token from localStorage with key: {TokenKey}", TokenKey);
                var token = await _localStorage.GetItemAsync<string>(TokenKey);
                _logger.LogDebug("Retrieved token from localStorage: {HasToken}", !string.IsNullOrEmpty(token));
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve token from storage");
                return null;
            }
        }

        public async ValueTask SetTokenAsync(string token)
        {
            try
            {
                _logger.LogDebug("Attempting to store token in localStorage: {HasToken}", !string.IsNullOrEmpty(token));

                if (string.IsNullOrEmpty(token))
                {
                    await RemoveTokenAsync();
                    return;
                }

                await _localStorage.SetItemAsync(TokenKey, token);
                _logger.LogDebug("Token stored successfully in localStorage");

                // Verify it was stored
                var verifyToken = await _localStorage.GetItemAsync<string>(TokenKey);
                _logger.LogDebug("Token verification: stored={Stored}", !string.IsNullOrEmpty(verifyToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store token");
                throw;
            }
        }

        public async ValueTask RemoveTokenAsync()
        {
            try
            {
                _logger.LogDebug("Removing token from localStorage");
                await _localStorage.RemoveItemAsync(TokenKey);
                _logger.LogDebug("Token removed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove token");
            }
        }

        public async ValueTask<bool> HasTokenAsync()
        {
            var token = await GetTokenAsync();
            var hasToken = !string.IsNullOrEmpty(token);
            _logger.LogDebug("HasToken check: {HasToken}", hasToken);
            return hasToken;
        }
    }
}
