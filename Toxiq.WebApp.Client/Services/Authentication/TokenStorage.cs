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
        }

        public async ValueTask<string> GetTokenAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<string>(TokenKey);
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
                if (string.IsNullOrEmpty(token))
                {
                    await RemoveTokenAsync();
                    return;
                }

                await _localStorage.SetItemAsync(TokenKey, token);
                _logger.LogDebug("Token stored successfully");
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
            return !string.IsNullOrEmpty(token);
        }
    }
}
