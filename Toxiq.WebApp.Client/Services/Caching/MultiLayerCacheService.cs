using Blazored.LocalStorage;
using Microsoft.Extensions.Caching.Memory;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Toxiq.WebApp.Client.Services.Caching
{

    public class MultiLayerCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<MultiLayerCacheService> _logger;

        private readonly MemoryCacheEntryOptions _defaultMemoryOptions = new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.Normal
        };

        public MultiLayerCacheService(
            IMemoryCache memoryCache,
            ILocalStorageService localStorage,
            ILogger<MultiLayerCacheService> logger)
        {
            _memoryCache = memoryCache;
            _localStorage = localStorage;
            _logger = logger;
        }

        public async ValueTask<T> GetAsync<T>(string key)
        {
            // Layer 1: Memory cache (fastest)
            if (_memoryCache.TryGetValue(key, out T memoryValue))
            {
                return memoryValue;
            }

            // Layer 2: Local storage (persistent)
            try
            {
                var cacheItem = await _localStorage.GetItemAsync<CacheItem<T>>($"cache_{key}");
                if (cacheItem != null && cacheItem.ExpiresAt > DateTime.UtcNow)
                {
                    // Promote to memory cache
                    _memoryCache.Set(key, cacheItem.Value, _defaultMemoryOptions);
                    return cacheItem.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read from local storage for key {Key}", key);
            }

            return default(T);
        }

        public async ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var expirationTime = expiration ?? TimeSpan.FromMinutes(30);

            // Set in memory cache
            _memoryCache.Set(key, value, expirationTime);

            // Set in local storage
            try
            {
                var cacheItem = new CacheItem<T>
                {
                    Value = value,
                    ExpiresAt = DateTime.UtcNow.Add(expirationTime)
                };
                await _localStorage.SetItemAsync($"cache_{key}", cacheItem);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write to local storage for key {Key}", key);
            }
        }

        public async ValueTask<T> GetOrSetAsync<T>(string key, Func<ValueTask<T>> factory, TimeSpan? expiration = null)
        {
            var cached = await GetAsync<T>(key);
            if (cached != null && !cached.Equals(default(T)))
            {
                return cached;
            }

            var value = await factory();
            if (value != null && !value.Equals(default(T)))
            {
                await SetAsync(key, value, expiration);
            }

            return value;
        }

        public async ValueTask RemoveAsync(string key)
        {
            _memoryCache.Remove(key);

            try
            {
                await _localStorage.RemoveItemAsync($"cache_{key}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove from local storage for key {Key}", key);
            }
        }

        public async ValueTask RemovePatternAsync(string pattern)
        {
            // Memory cache pattern removal (basic implementation)
            var field = typeof(MemoryCache).GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field?.GetValue(_memoryCache) is IDictionary store)
            {
                var keysToRemove = store.Keys
                    .OfType<string>()
                    .Where(k => IsMatch(k, pattern))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _memoryCache.Remove(key);
                }
            }

            // Local storage pattern removal would require enumerating all keys
            // This is a simplified implementation
        }

        public ValueTask ClearExpiredAsync()
        {
            // Memory cache handles this automatically
            // Local storage cleanup would need to be implemented
            return ValueTask.CompletedTask;
        }

        private static bool IsMatch(string text, string pattern)
        {
            return pattern.Contains('*')
                ? Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$")
                : text.Contains(pattern);
        }
    }
    public class CacheItem<T>
    {
        public T Value { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
