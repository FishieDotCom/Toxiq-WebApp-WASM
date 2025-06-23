namespace Toxiq.WebApp.Client.Services.Caching
{
    public interface ICacheService
    {
        ValueTask<T> GetAsync<T>(string key);
        ValueTask SetAsync<T>(string key, T value, TimeSpan? expiration = null);
        ValueTask<T> GetOrSetAsync<T>(string key, Func<ValueTask<T>> factory, TimeSpan? expiration = null);
        ValueTask RemoveAsync(string key);
        ValueTask RemovePatternAsync(string pattern);
        ValueTask ClearExpiredAsync();
    }
}
