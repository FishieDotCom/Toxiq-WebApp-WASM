namespace Toxiq.WebApp.Client.Services.Caching
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
        Task RemoveAsync(string key);
        Task ClearAsync();
        Task<bool> ExistsAsync(string key);

        // Pattern-based operations
        Task RemoveByPatternAsync(string pattern);
        Task<List<string>> GetKeysAsync();

        // Cleanup operations
        Task CleanupExpiredAsync();
        Task CleanupOldAsync(int daysToKeep = 30);
    }
}
