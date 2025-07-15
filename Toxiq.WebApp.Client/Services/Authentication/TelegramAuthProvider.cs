using Blazored.LocalStorage;
using TelegramApps.Blazor.Services;
using Toxiq.Mobile.Dto;

namespace Toxiq.WebApp.Client.Services.Authentication
{
    public class TelegramAuthProvider : IAuthenticationProvider
    {
        private readonly ITelegramWebAppService _telegramService;
        private readonly ILocalStorageService _localStorage;
        private readonly IApiService _apiService;
        private readonly ILogger<TelegramAuthProvider> _logger;

        public TelegramAuthProvider(
            ITelegramWebAppService telegramService,
            ILocalStorageService localStorage,
            IApiService apiService,
            ILogger<TelegramAuthProvider> logger)
        {
            _telegramService = telegramService;
            _localStorage = localStorage;
            _apiService = apiService;
            _logger = logger;
        }

        public string ProviderName => "Telegram";

        public bool IsAvailable => true; // Always try when in Telegram context

        public async ValueTask<bool> CanAutoLoginAsync()
        {
            try
            {
                var isTelegramWebApp = await _telegramService.IsAvailableAsync();
                if (!isTelegramWebApp)
                {
                    _logger.LogDebug("Not in Telegram WebApp context");
                    return false;
                }

                var initData = await _telegramService.GetInitDataAsync();
                var hasInitData = !string.IsNullOrWhiteSpace(initData?.Hash);
                _logger.LogDebug("Telegram WebApp auto-login available: {HasInitData}", hasInitData);
                return hasInitData;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking Telegram auto-login availability");
                return false;
            }
        }

        public async ValueTask<AuthenticationResult> LoginAsync(LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Attempting Telegram auto-login...");

                var initData = await _telegramService.GetRawInitDataAsync();
                if (string.IsNullOrWhiteSpace(initData))
                {
                    _logger.LogWarning("No Telegram init data available");
                    return new AuthenticationResult(false, ErrorMessage: "Telegram authentication data not available");
                }

                var maxRetries = 3;
                var retryCount = 0;

                while (retryCount < maxRetries)
                {
                    try
                    {
                        var loginDto = new LoginDto
                        {
                            PhoneNumber = "",
                            OTP = initData
                        };

                        var loginResult = await _apiService.AuthService.TG_Login(loginDto);

                        if (!string.IsNullOrEmpty(loginResult.token) && loginResult.token != "NA")
                        {
                            await _localStorage.SetItemAsStringAsync("token", loginResult.token);
                            var userProfile = await _apiService.UserService.GetMe();
                            _logger.LogInformation("Telegram auto-login successful for user: {Username}", userProfile?.UserName);
                            return new AuthenticationResult(
                                IsSuccess: true,
                                Token: loginResult.token,
                                UserProfile: userProfile,
                                RequiresAdditionalSetup: loginResult.IsNew
                            );
                        }
                        else
                        {
                            _logger.LogWarning("Login attempt {RetryCount} failed: Invalid token response", retryCount + 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Login attempt {RetryCount} failed with exception", retryCount + 1);
                    }

                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        await Task.Delay(1000);
                    }
                }

                return new AuthenticationResult(false, ErrorMessage: "Telegram authentication failed after multiple attempts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram authentication error");
                return new AuthenticationResult(false, ErrorMessage: "Telegram authentication error");
            }
        }

        public async ValueTask<bool> ValidateTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    return false;
                // Use the API service to validate the token
                //var isValid = await _apiService.AuthService.ValidateTokenAsync(token);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return false;
            }
        }

        public async ValueTask LogoutAsync()
        {
            try
            {
                await _localStorage.RemoveItemAsync("token");
                await _telegramService.CloseAsync();
                _logger.LogInformation("Telegram logout completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Telegram logout");
            }
        }
    }
}