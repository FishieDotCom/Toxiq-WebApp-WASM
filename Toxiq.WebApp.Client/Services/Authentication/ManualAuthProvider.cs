using Toxiq.Mobile.Dto;

namespace Toxiq.WebApp.Client.Services.Authentication
{

    public class ManualAuthProvider : IAuthenticationProvider
    {
        private readonly IApiService _apiService;
        private readonly ITokenStorage _tokenStorage;
        private readonly ILogger<ManualAuthProvider> _logger;

        public ManualAuthProvider(IApiService apiService, ITokenStorage tokenStorage, ILogger<ManualAuthProvider> logger)
        {
            _apiService = apiService;
            _tokenStorage = tokenStorage;
            _logger = logger;
        }

        public string ProviderName => "Manual";
        public bool IsAvailable => true;

        public ValueTask<bool> CanAutoLoginAsync()
        {
            return ValueTask.FromResult(false); // Manual login always requires user input
        }

        // Toxiq.WebApp.Client/Services/Authentication/ManualAuthProvider.cs
        public async ValueTask<AuthenticationResult> LoginAsync(LoginRequest request)
        {
            try
            {
                var loginDto = new LoginDto
                {
                    PhoneNumber = "", // Empty like mobile app
                    OTP = request.Credential
                };

                var response = await _apiService.AuthService.Login(loginDto);

                if (response.token == "NA" || string.IsNullOrEmpty(response.token))
                {
                    return new AuthenticationResult(false, ErrorMessage: "Invalid login token");
                }

                await _tokenStorage.SetTokenAsync(response.token);

                return new AuthenticationResult(
                    IsSuccess: true,
                    Token: response.token,
                    UserProfile: response.Profile,
                    RequiresAdditionalSetup: response.IsNew
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for provider {Provider}", ProviderName);
                return new AuthenticationResult(false, ErrorMessage: "Login failed. Please try again.");
            }
        }

        public async ValueTask<bool> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                // Set token temporarily to test it
                var currentToken = await _tokenStorage.GetTokenAsync();
                await _tokenStorage.SetTokenAsync(token);

                var isValid = await _apiService.AuthService.CheckHeartBeat();

                if (!isValid && currentToken != null)
                {
                    await _tokenStorage.SetTokenAsync(currentToken); // Restore previous token
                }

                return isValid;
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask LogoutAsync()
        {
            await _tokenStorage.RemoveTokenAsync();
        }
    }
}
