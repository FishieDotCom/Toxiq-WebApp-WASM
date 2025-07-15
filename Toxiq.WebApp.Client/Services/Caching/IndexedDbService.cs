// Toxiq.WebApp.Client/Services/Caching/IndexedDbService.cs
using Microsoft.JSInterop;
using System.Text.Json;

namespace Toxiq.WebApp.Client.Services.Caching
{
    /// <summary>
    /// Cache service using IndexedDB for all data storage
    /// Simple, persistent, and mirrors mobile app storage patterns
    /// </summary>
    public class IndexedDbCacheService : ICacheService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<IndexedDbCacheService> _logger;

        public IndexedDbCacheService(IJSRuntime jsRuntime, ILogger<IndexedDbCacheService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                // Get raw data from IndexedDB
                var json = await _jsRuntime.InvokeAsync<string>("toxiqIndexedDb.getItem", key);

                if (string.IsNullOrEmpty(json))
                {
                    return default(T);
                }

                // Deserialize cache entry
                var cacheEntry = JsonSerializer.Deserialize<CacheEntry>(json);

                if (cacheEntry == null)
                {
                    return default(T);
                }

                // Check expiration
                if (cacheEntry.ExpiresAt.HasValue && cacheEntry.ExpiresAt.Value <= DateTime.UtcNow)
                {
                    await RemoveAsync(key);
                    return default(T);
                }

                // Deserialize the actual value
                return JsonSerializer.Deserialize<T>(cacheEntry.Data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cache item for key: {Key}", key);
                return default(T);
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            try
            {
                // Create cache entry
                var cacheEntry = new CacheEntry
                {
                    Data = JsonSerializer.Serialize(value),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null
                };

                // Serialize and store in IndexedDB
                var json = JsonSerializer.Serialize(cacheEntry);
                await _jsRuntime.InvokeVoidAsync("toxiqIndexedDb.setItem", key, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set cache item for key: {Key}", key);
            }
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            // Try to get existing value
            var existing = await GetAsync<T>(key);
            if (existing != null && !existing.Equals(default(T)))
            {
                return existing;
            }

            // Create new value
            var value = await factory();

            // Cache the new value if not null/default
            if (value != null && !value.Equals(default(T)))
            {
                await SetAsync(key, value, expiration);
            }

            return value;
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("toxiqIndexedDb.removeItem", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove cache item for key: {Key}", key);
            }
        }

        public async Task ClearAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("toxiqIndexedDb.clear");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear cache");
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("toxiqIndexedDb.containsKey", key);
            }
            catch
            {
                return false;
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var allKeys = await GetKeysAsync();
                var keysToRemove = allKeys
                    .Where(k => k.Contains(pattern) ||
                               (pattern.Contains("*") && IsPatternMatch(k, pattern)))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    await RemoveAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove cache items by pattern: {Pattern}", pattern);
            }
        }

        public async Task<List<string>> GetKeysAsync()
        {
            try
            {
                // Get all items and extract keys
                var allItems = await _jsRuntime.InvokeAsync<object[]>("toxiqIndexedDb.getAllItems");
                return allItems?.Select(item =>
                {
                    var jsonElement = (JsonElement)item;
                    return jsonElement.GetProperty("key").GetString();
                })
                .Where(key => key != null)
                .ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cache keys");
                return new List<string>();
            }
        }

        public async Task CleanupExpiredAsync()
        {
            try
            {
                var allKeys = await GetKeysAsync();

                foreach (var key in allKeys)
                {
                    // This will automatically remove expired items
                    await GetAsync<object>(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup expired cache items");
            }
        }

        public async Task CleanupOldAsync(int daysToKeep = 30)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("toxiqIndexedDb.cleanupOldItems", daysToKeep);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup old cache items");
            }
        }

        private static bool IsPatternMatch(string text, string pattern)
        {
            if (!pattern.Contains("*")) return text.Contains(pattern);

            var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(text, regexPattern);
        }
    }

    /// <summary>
    /// Cache entry wrapper for IndexedDB storage
    /// </summary>
    public class CacheEntry
    {
        public string Data { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}