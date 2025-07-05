using Microsoft.JSInterop;
using System.Text.Json;

namespace Toxiq.WebApp.Client.Services.JavaScript
{
    public interface ITelegramAuthJsInvoker
    {
        // Telegram WebApp Detection & Initialization
        Task<bool> IsTelegramWebAppAsync();
        Task<bool> HasValidInitDataAsync();
        Task<string> GetInitDataAsync();
        Task<string> InitializeTelegramWebAppAsync();

        // Navigation & UI Controls
        Task ShowBackButtonAsync();
        Task HideBackButtonAsync();
        Task SetBackButtonCallbackAsync(string callbackFunction);
        Task CloseWebAppAsync();

        // Cloud Storage
        Task SaveToCloudAsync(string key, string value);
        Task<string> LoadFromCloudAsync(string key);

        // User Interface
        Task ShowAlertAsync(string message);
        Task ShareToTelegramAsync(string url);

        // User Information
        Task<TelegramUserInfo?> GetTelegramUserInfoAsync();

        // Platform Detection
        Task<PlatformInfo> GetPlatformInfoAsync();

        // LocalStorage Helpers (Legacy Support)
        Task SetLocalStorageAsync(string key, string value);
        Task<string> GetLocalStorageAsync(string key);

        // Token Management (Legacy Support)
        Task SetTokenAsync(string token);
        Task<string> GetTokenAsync();
    }

    public class TelegramAuthJsInvoker : ITelegramAuthJsInvoker
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<TelegramAuthJsInvoker> _logger;

        public TelegramAuthJsInvoker(IJSRuntime jsRuntime, ILogger<TelegramAuthJsInvoker> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        // Telegram WebApp Detection & Initialization
        public async Task<bool> IsTelegramWebAppAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("telegramAuthUtils.isTelegramWebApp");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if Telegram WebApp");
                return false;
            }
        }

        public async Task<bool> HasValidInitDataAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("telegramAuthUtils.hasValidInitData");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Telegram init data validity");
                return false;
            }
        }

        public async Task<string> GetInitDataAsync()
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<string>("telegramAuthUtils.getInitData");
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Telegram init data");
                return string.Empty;
            }
        }

        public async Task<string> InitializeTelegramWebAppAsync()
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<string>("telegramAuthUtils.initializeTelegramWebApp");
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Telegram WebApp");
                return string.Empty;
            }
        }

        // Navigation & UI Controls
        public async Task ShowBackButtonAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.showBackButton");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing Telegram back button");
            }
        }

        public async Task HideBackButtonAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.hideBackButton");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding Telegram back button");
            }
        }

        public async Task SetBackButtonCallbackAsync(string callbackFunction)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.setBackButtonCallback", callbackFunction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting Telegram back button callback");
            }
        }

        public async Task CloseWebAppAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.closeWebApp");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing Telegram WebApp");
            }
        }

        // Cloud Storage
        public async Task SaveToCloudAsync(string key, string value)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.saveToCloud", key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving to Telegram cloud storage");
            }
        }

        public async Task<string> LoadFromCloudAsync(string key)
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<string>("telegramAuthUtils.loadFromCloud", key);
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading from Telegram cloud storage");
                return string.Empty;
            }
        }

        // User Interface
        public async Task ShowAlertAsync(string message)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.showAlert", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing Telegram alert");
            }
        }

        public async Task ShareToTelegramAsync(string url)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.shareToTele", url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sharing to Telegram");
            }
        }

        // User Information
        public async Task<TelegramUserInfo?> GetTelegramUserInfoAsync()
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<JsonElement?>("telegramAuthUtils.getTelegramUserInfo");

                if (result == null || result.Value.ValueKind == JsonValueKind.Null)
                    return null;

                var jsonElement = result.Value;
                return new TelegramUserInfo
                {
                    Id = jsonElement.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0,
                    FirstName = jsonElement.TryGetProperty("firstName", out var firstNameProp) ? firstNameProp.GetString() : null,
                    LastName = jsonElement.TryGetProperty("lastName", out var lastNameProp) ? lastNameProp.GetString() : null,
                    Username = jsonElement.TryGetProperty("username", out var usernameProp) ? usernameProp.GetString() : null,
                    LanguageCode = jsonElement.TryGetProperty("languageCode", out var langProp) ? langProp.GetString() : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Telegram user info");
                return null;
            }
        }

        // Platform Detection
        public async Task<PlatformInfo> GetPlatformInfoAsync()
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<JsonElement>("getPlatformInfo");

                return new PlatformInfo
                {
                    IsTelegramMiniApp = result.TryGetProperty("isTelegramMiniApp", out var isTelegramProp) && isTelegramProp.GetBoolean(),
                    IsDesktop = result.TryGetProperty("isDesktop", out var isDesktopProp) && isDesktopProp.GetBoolean(),
                    IsMobile = result.TryGetProperty("isMobile", out var isMobileProp) && isMobileProp.GetBoolean(),
                    UserAgent = result.TryGetProperty("userAgent", out var userAgentProp) ? userAgentProp.GetString() : null,
                    ViewportWidth = result.TryGetProperty("viewportWidth", out var widthProp) ? widthProp.GetInt32() : 0,
                    ViewportHeight = result.TryGetProperty("viewportHeight", out var heightProp) ? heightProp.GetInt32() : 0,
                    HasTouch = result.TryGetProperty("hasTouch", out var hasTouchProp) && hasTouchProp.GetBoolean()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting platform info");
                return new PlatformInfo();
            }
        }

        // LocalStorage Helpers (Legacy Support)
        public async Task SetLocalStorageAsync(string key, string value)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("LSset", key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting localStorage");
            }
        }

        public async Task<string> GetLocalStorageAsync(string key)
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<string>("LSget", key);
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localStorage");
                return string.Empty;
            }
        }

        // Token Management (Legacy Support)
        public async Task SetTokenAsync(string token)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("setToken", token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting token");
            }
        }

        public async Task<string> GetTokenAsync()
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<string>("getToken");
                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token");
                return string.Empty;
            }
        }
    }

    // Supporting DTOs for type safety
    public class TelegramUserInfo
    {
        public long Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? LanguageCode { get; set; }
    }

    public class PlatformInfo
    {
        public bool IsTelegramMiniApp { get; set; }
        public bool IsDesktop { get; set; }
        public bool IsMobile { get; set; }
        public string? UserAgent { get; set; }
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }
        public bool HasTouch { get; set; }
    }
}