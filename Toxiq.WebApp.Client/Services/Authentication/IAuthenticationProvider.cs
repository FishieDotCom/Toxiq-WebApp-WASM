namespace Toxiq.WebApp.Client.Services.Authentication
{
    public interface IAuthenticationProvider
    {
        string ProviderName { get; }
        bool IsAvailable { get; }
        ValueTask<bool> CanAutoLoginAsync();
        ValueTask<AuthenticationResult> LoginAsync(LoginRequest request);
        ValueTask<bool> ValidateTokenAsync(string token);
        ValueTask LogoutAsync();
    }
}
