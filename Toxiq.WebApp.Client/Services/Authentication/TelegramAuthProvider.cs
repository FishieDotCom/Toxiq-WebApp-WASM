using Blazored.LocalStorage;
using Microsoft.JSInterop;
using System.Text.Json;
using Toxiq.Mobile.Dto;

namespace Toxiq.WebApp.Client.Services.Authentication
{
    public class TelegramAuthProvider : IAuthenticationProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILocalStorageService _localStorage;
        private readonly IApiService _apiService;
        private readonly ILogger<TelegramAuthProvider> _logger;

        public TelegramAuthProvider(
            IJSRuntime jsRuntime,
            ILocalStorageService localStorage,
            IApiService apiService,
            ILogger<TelegramAuthProvider> logger)
        {
            _jsRuntime = jsRuntime;
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
                // Check if we're in a Telegram WebApp context
                var result = await _jsRuntime.InvokeAsync<JsonElement>("window.toxiqPlatform.detect");

                var isTelegramWebApp = result.TryGetProperty("isTelegramMiniApp", out var isTelegramMiniAppProperty)
                    && isTelegramMiniAppProperty.GetBoolean();

                if (!isTelegramWebApp)
                {
                    _logger.LogDebug("Not in Telegram WebApp context");
                    return false;
                }

                // Check if Telegram WebApp is properly initialized
                var hasInitData = await _jsRuntime.InvokeAsync<bool>("telegramAuthUtils.hasValidInitData");
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

                // Get Telegram WebApp init data
                var initData = await _jsRuntime.InvokeAsync<string>("telegramAuthUtils.getInitData");

                if (string.IsNullOrWhiteSpace(initData))
                {
                    _logger.LogWarning("No Telegram init data available");
                    return new AuthenticationResult(false, ErrorMessage: "Telegram authentication data not available");
                }

                // Attempt login with Telegram data (with retry logic like mobile app)
                var maxRetries = 3;
                var retryCount = 0;

                while (retryCount < maxRetries)
                {
                    try
                    {
                        var loginDto = new LoginDto
                        {
                            PhoneNumber = "", // Empty like mobile app uses
                            OTP = initData
                        };

                        var loginResult = await _apiService.AuthService.Login(loginDto);

                        if (!string.IsNullOrEmpty(loginResult.token) && loginResult.token != "NA")
                        {
                            // Store token persistently
                            await _localStorage.SetItemAsStringAsync("token", loginResult.token);

                            // Get user profile
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

                    // Small delay before retry (like mobile app pattern)
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
                // Clear stored token
                await _localStorage.RemoveItemAsync("token");

                // Close Telegram WebApp if needed
                await _jsRuntime.InvokeVoidAsync("telegramAuthUtils.closeWebApp").ConfigureAwait(false);

                _logger.LogInformation("Telegram logout completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Telegram logout");
            }
        }
    }
}