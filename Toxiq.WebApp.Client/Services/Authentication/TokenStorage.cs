using Toxiq.WebApp.Client.Services.Caching;

namespace Toxiq.WebApp.Client.Services.Authentication
{
    public interface ITokenStorage
    {
        Task ClearTokensAsync();
        Task<string?> GetAccessTokenAsync();
        Task<string?> GetRefreshTokenAsync();
        ValueTask<bool> HasTokenAsync();
        Task<bool> HasValidTokenAsync();
        Task SetAccessTokenAsync(string token);
        Task SetRefreshTokenAsync(string token);
    }

    public class LocalStorageTokenStorage : ITokenStorage
    {
        private readonly ILocalSecureStorage _secureStorage;
        private const string ACCESS_TOKEN_KEY = "token";
        private const string REFRESH_TOKEN_KEY = "refresh_token";
        private const string USER_PROFILE_KEY = "user_profile";

        public LocalStorageTokenStorage(ILocalSecureStorage secureStorage)
        {
            _secureStorage = secureStorage;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            return await _secureStorage.GetAsync<string>(ACCESS_TOKEN_KEY);
        }

        public async Task SetAccessTokenAsync(string token)
        {
            await _secureStorage.SetAsync(ACCESS_TOKEN_KEY, token);
        }

        public async Task<string?> GetRefreshTokenAsync()
        {
            return await _secureStorage.GetAsync<string>(REFRESH_TOKEN_KEY);
        }

        public async Task SetRefreshTokenAsync(string token)
        {
            await _secureStorage.SetAsync(REFRESH_TOKEN_KEY, token);
        }

        public async Task ClearTokensAsync()
        {
            await _secureStorage.RemoveAsync(ACCESS_TOKEN_KEY);
            await _secureStorage.RemoveAsync(REFRESH_TOKEN_KEY);
            await _secureStorage.RemoveAsync(USER_PROFILE_KEY);
        }

        public async Task<bool> HasValidTokenAsync()
        {
            var token = await GetAccessTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        public ValueTask<bool> HasTokenAsync()
        {
            throw new NotImplementedException();
        }
    }
}
