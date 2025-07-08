using Blazored.LocalStorage;

namespace Toxiq.WebApp.Client.Services.Caching
{
    /// <summary>
    /// Secure storage interface for sensitive data (tokens, auth state)
    /// Uses LocalStorage for immediate access to auth data
    /// </summary>
    public interface ILocalSecureStorage
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value);
        Task RemoveAsync(string key);
        Task ClearAsync();
        Task<bool> ContainsKeyAsync(string key);
    }

    /// <summary>
    /// LocalStorage implementation for tokens and auth state
    /// Simple wrapper around Blazored.LocalStorage
    /// </summary>
    public class LocalSecureStorage : ILocalSecureStorage
    {
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<LocalSecureStorage> _logger;
        private const string PREFIX = "toxiq_secure_";

        public LocalSecureStorage(ILocalStorageService localStorage, ILogger<LocalSecureStorage> logger)
        {
            _localStorage = localStorage;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                return await _localStorage.GetItemAsync<T>($"{PREFIX}{key}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get secure storage item for key: {Key}", key);
                return default(T);
            }
        }

        public async Task SetAsync<T>(string key, T value)
        {
            try
            {
                await _localStorage.SetItemAsync($"{PREFIX}{key}", value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set secure storage item for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _localStorage.RemoveItemAsync($"{PREFIX}{key}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove secure storage item for key: {Key}", key);
            }
        }

        public async Task ClearAsync()
        {
            try
            {
                // Get all keys that start with our prefix
                var length = await _localStorage.LengthAsync();
                var keysToRemove = new List<string>();

                for (int i = 0; i < length; i++)
                {
                    var key = await _localStorage.KeyAsync(i);
                    if (key?.StartsWith(PREFIX) == true)
                    {
                        keysToRemove.Add(key);
                    }
                }

                // Remove all secure storage items
                foreach (var key in keysToRemove)
                {
                    await _localStorage.RemoveItemAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear secure storage");
            }
        }

        public async Task<bool> ContainsKeyAsync(string key)
        {
            try
            {
                return await _localStorage.ContainKeyAsync($"{PREFIX}{key}");
            }
            catch
            {
                return false;
            }
        }
    }
}
