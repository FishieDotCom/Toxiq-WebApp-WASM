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

        public async ValueTask<AuthenticationResult> LoginAsync(LoginRequest request)
        {
            try
            {
                _logger.LogDebug("Starting login process with credential length: {Length}", request.Credential?.Length);

                var loginDto = new LoginDto
                {
                    PhoneNumber = "", // Empty string like mobile app
                    OTP = request.Credential
                };

                var response = await _apiService.AuthService.Login(loginDto);
                _logger.LogDebug("Login API response received: token={TokenStatus}",
                    response.token == "NA" ? "INVALID" : "VALID");

                if (response.token == "NA" || string.IsNullOrEmpty(response.token))
                {
                    return new AuthenticationResult(false, ErrorMessage: "Invalid login token");
                }

                _logger.LogDebug("Storing token...");
                await _tokenStorage.SetTokenAsync(response.token);
                _logger.LogDebug("Token stored, verifying...");

                // Verify token was stored
                var storedToken = await _tokenStorage.GetTokenAsync();
                _logger.LogDebug("Token verification: stored={Stored}", !string.IsNullOrEmpty(storedToken));

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

                //var isValid = await _apiService.AuthService.CheckHeartBeat();

                if (currentToken != null)
                {
                    await _tokenStorage.SetTokenAsync(currentToken); // Restore previous token
                }

                return true;
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
