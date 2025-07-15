// Toxiq.WebApp.Client/Services/Api/UserService.cs
// Enhanced UserService implementation matching mobile app patterns with Dhivehi support

using System.Text.RegularExpressions;
using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Caching;

namespace Toxiq.WebApp.Client.Services.Api
{
    public interface IUserService
    {
        ValueTask<UserProfile> GetMe(bool force = false);
        ValueTask<UserProfile> GetUser(string username);
        ValueTask<bool> CheckUsername(string username);
        ValueTask<bool> ChangeUsername(string username);
        ValueTask EditProfile(UserProfile profile);
        ValueTask<List<BasePost>> GetUserPosts(string username, bool includeReplies = false);
        ValueTask<List<BasePost>> GetUserWallPosts(string username);
        bool ValidateUsername(string username, out string errorMessage);
        bool ValidateUsernameLength(string username);
        bool ValidateUsernameCharacters(string username);
        void InvalidateCache();
        void InvalidateUserCache(string username);
    }

    public class UserService : IUserService
    {
        private readonly OptimizedApiService _api;
        private readonly ILogger<UserService> _logger;

        // Dhivehi/Thaana character pattern (matching mobile app exactly)
        private const string DhivehiPattern = @"ހށނރބޅކއވމފދތލގޏސޑޒޓޔޕޖޗޘޙޚޛޜޝޞޟޠޡޢޣޤޥަާިީުޫެޭޮޯްޱ";
        private const string UsernamePattern = @"^[a-zA-Z0-9_" + DhivehiPattern + @"]{4,15}$";
        private readonly ICacheService _cache; // IndexedDB

        public UserService(OptimizedApiService api, ICacheService cacheService, ILogger<UserService> logger = null)
        {
            _api = api;
            _logger = logger;
            _cache = cacheService;
        }

        public async ValueTask<UserProfile> GetMe(bool force = false)
        {
            if (force)
            {
                var user = await _api.GetAsync<UserProfile>("User/GetMe");
                await _cache.SetAsync("User/GetMe", user, TimeSpan.FromMinutes(30));
                return user;
            }
            else
            {
                return await _cache.GetOrCreateAsync<UserProfile>("User/GetMe", async () =>
                 {
                     // Fetch user profile with caching
                     return await _api.GetAsync<UserProfile>("User/GetMe");
                 }, TimeSpan.FromMinutes(30));
            }
        }

        public async ValueTask<UserProfile> GetUser(string username)
        {
            // Use cached version if available, otherwise fetch from API
            return await _cache.GetOrCreateAsync<UserProfile>($"User/GetUser/{username}", async () =>
            {
                return await _api.GetAsync<UserProfile>($"User/GetUser/{username}");
            }, TimeSpan.FromMinutes(15));
        }

        public async ValueTask<bool> CheckUsername(string username)
        {
            try
            {
                _logger?.LogDebug("Checking username availability: {Username}", username);

                // Client-side validation first (matching mobile app)
                if (!ValidateUsername(username, out string errorMessage))
                {
                    _logger?.LogDebug("Username validation failed: {Error}", errorMessage);
                    return false;
                }

                // API call to check availability (matching mobile endpoint)
                var response = await _api.GetRawAsync($"User/CheckUsername?username={Uri.EscapeDataString(username)}");
                var isAvailable = response.IsSuccessStatusCode;

                _logger?.LogDebug("Username {Username} availability: {Available}", username, isAvailable);
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking username availability for: {Username}", username);
                return false;
            }
        }

        public async ValueTask<bool> ChangeUsername(string username)
        {
            try
            {
                _logger?.LogDebug("Changing username to: {Username}", username);

                // Client-side validation (matching mobile app)
                if (!ValidateUsername(username, out string errorMessage))
                {
                    _logger?.LogWarning("Username change validation failed: {Error}", errorMessage);
                    return false;
                }

                // API call to change username (matching mobile endpoint)
                var response = await _api.GetRawAsync($"User/ChangeUsername?username={Uri.EscapeDataString(username)}");
                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    // Invalidate cache after successful username change
                    InvalidateCache();
                    _logger?.LogInformation("Username successfully changed to: {Username}", username);
                }
                else
                {
                    _logger?.LogWarning("Username change failed for: {Username}", username);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error changing username to: {Username}", username);
                return false;
            }
        }

        public async ValueTask EditProfile(UserProfile profile)
        {
            try
            {
                _logger?.LogDebug("Editing user profile");
                await _api.PostRawAsync("User/EditUserProfile", profile);

                // Invalidate cache after profile update
                InvalidateCache();

                await GetMe(true); // Refresh cached profile
                _logger?.LogInformation("Profile successfully updated");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating user profile");
                throw;
            }
        }

        public ValueTask<List<BasePost>> GetUserPosts(string username, bool includeReplies = false) =>
            _api.GetAsync<List<BasePost>>($"User/GetUserPosts/{username}");

        public ValueTask<List<BasePost>> GetUserWallPosts(string username) =>
            _api.GetAsync<List<BasePost>>($"User/GetUserWallPosts/{username}");

        // Username validation methods (matching mobile app exactly)
        public bool ValidateUsername(string username, out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Username cannot be empty";
                return false;
            }

            // Check if username starts with "user" (mobile app restriction)
            if (username.StartsWith("user", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Invalid Username";
                return false;
            }

            // Check username length (mobile app: 4-15 characters)
            if (!ValidateUsernameLength(username))
            {
                errorMessage = "Username must be between 4-15 characters";
                return false;
            }

            // Check allowed characters (mobile app: alphanumeric, underscore, Dhivehi/Thaana)
            if (!ValidateUsernameCharacters(username))
            {
                errorMessage = "Username can only contain alphanumeric characters, Thaana and _";
                return false;
            }

            return true;
        }

        public bool ValidateUsernameLength(string username)
        {
            return !string.IsNullOrEmpty(username) && username.Length >= 4 && username.Length <= 15;
        }

        public bool ValidateUsernameCharacters(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            // Mobile app pattern with Dhivehi support
            Regex regex = new Regex(UsernamePattern);
            return regex.IsMatch(username);
        }

        public void InvalidateCache()
        {
            // Invalidate user-related cache entries            
            _cache.RemoveByPatternAsync("User/GetUser/");
            _logger?.LogDebug("User cache invalidated");
        }

        public void InvalidateUserCache(string username)
        {
            // Invalidate specific user cache
            //_api.InvalidateCachePattern($"User/GetUser/{username}");
            //_api.InvalidateCachePattern($"User/GetUserPosts/{username}");
            //_api.InvalidateCachePattern($"User/GetUserWallPosts/{username}");
            _logger?.LogDebug("Cache invalidated for user: {Username}", username);
        }

        // Helper method for username validation with detailed error reporting
        public ValidationResult ValidateUsernameDetailed(string username)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(username))
            {
                result.IsValid = false;
                result.ErrorMessage = "Username cannot be empty";
                result.ErrorCode = "EMPTY_USERNAME";
                return result;
            }

            if (username.StartsWith("user", StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.ErrorMessage = "Invalid Username";
                result.ErrorCode = "INVALID_PREFIX";
                return result;
            }

            if (username.Length < 4)
            {
                result.IsValid = false;
                result.ErrorMessage = "Username too short (minimum 4 characters)";
                result.ErrorCode = "TOO_SHORT";
                return result;
            }

            if (username.Length > 15)
            {
                result.IsValid = false;
                result.ErrorMessage = "Username too long (maximum 15 characters)";
                result.ErrorCode = "TOO_LONG";
                return result;
            }

            if (!ValidateUsernameCharacters(username))
            {
                result.IsValid = false;
                result.ErrorMessage = "Username can only contain alphanumeric characters, Thaana and _";
                result.ErrorCode = "INVALID_CHARACTERS";
                return result;
            }

            result.IsValid = true;
            result.ErrorMessage = "";
            result.ErrorCode = "";
            return result;
        }

        // Static helper methods for use across components
        public static bool IsValidDhivehiCharacter(char c)
        {
            // Check if character is in Dhivehi/Thaana Unicode range
            return (c >= '\u0780' && c <= '\u07BF');
        }

        public static bool ContainsDhivehiCharacters(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Any(IsValidDhivehiCharacter);
        }

        public static string SanitizeUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
                return "";

            // Remove any characters that are not allowed
            var allowedChars = username.Where(c =>
                char.IsLetterOrDigit(c) ||
                c == '_' ||
                IsValidDhivehiCharacter(c)).ToArray();

            return new string(allowedChars);
        }
    }

    // Helper class for detailed validation results
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string ErrorCode { get; set; } = "";
    }

    // Extension methods for username handling
    public static class UsernameExtensions
    {
        public static bool IsValidToxiqUsername(this string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            if (username.StartsWith("user", StringComparison.OrdinalIgnoreCase))
                return false;

            if (username.Length < 4 || username.Length > 15)
                return false;

            const string pattern = @"^[a-zA-Z0-9_ހށނރބޅކއވމފދތލގޏސޑޒޓޔޕޖޗޘޙޚޛޜޝޞޟޠޡޢޣޤޥަާިީުޫެޭޮޯްޱ]{4,15}$";
            return Regex.IsMatch(username, pattern);
        }

        public static string GetUsernameValidationError(this string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return "Username cannot be empty";

            if (username.StartsWith("user", StringComparison.OrdinalIgnoreCase))
                return "Invalid Username";

            if (username.Length < 4)
                return "Username must be at least 4 characters";

            if (username.Length > 15)
                return "Username cannot exceed 15 characters";

            const string pattern = @"^[a-zA-Z0-9_ހށނރބޅކއވމފދތލގޏސޑޒޓޔޕޖޗޘޙޚޛޜޝޞޟޠޡޢޣޤޥަާިީުޫެޭޮޯްޱ]{4,15}$";
            if (!Regex.IsMatch(username, pattern))
                return "Username can only contain alphanumeric characters, Thaana and _";

            return "";
        }
    }
}