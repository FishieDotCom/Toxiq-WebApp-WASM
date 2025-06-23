namespace Toxiq.WebApp.Client.Services.Authentication
{

    // Future Telegram provider ready for implementation
    public class TelegramAuthProvider : IAuthenticationProvider
    {
        public string ProviderName => "Telegram";
        public bool IsAvailable => false; // Will be true when implemented

        public ValueTask<bool> CanAutoLoginAsync() => ValueTask.FromResult(true);

        public ValueTask<AuthenticationResult> LoginAsync(LoginRequest request)
        {
            // Future implementation
            throw new NotImplementedException("Telegram authentication will be implemented in Phase 2");
        }

        public ValueTask<bool> ValidateTokenAsync(string token) => ValueTask.FromResult(false);
        public ValueTask LogoutAsync() => ValueTask.CompletedTask;
    }
}
