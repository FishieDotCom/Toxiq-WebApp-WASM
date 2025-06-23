using Toxiq.Mobile.Dto;

public record LoginRequest(string Identifier, string Credential, Dictionary<string, object> AdditionalData = null);

public record AuthenticationResult(
    bool IsSuccess,
    string Token = null,
    string ErrorMessage = null,
    UserProfile UserProfile = null,
    bool RequiresAdditionalSetup = false
);
