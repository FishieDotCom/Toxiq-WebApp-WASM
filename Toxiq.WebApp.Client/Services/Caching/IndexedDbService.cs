// Toxiq.WebApp.Client/Services/Caching/IndexedDbService.cs
using Microsoft.JSInterop;
using System.Text.Json;

namespace Toxiq.WebApp.Client.Services.Caching
{
    /// <summary>
    /// IndexedDB service interface for client-side data persistence
    /// Replaces mobile app's MonkeyCache with web-compatible storage
    /// </summary>
    public interface IIndexedDbService
    {
        /// <summary>
        /// Store item in IndexedDB
        /// </summary>
        Task SetItemAsync<T>(string key, T value);

        /// <summary>
        /// Get item from IndexedDB
        /// </summary>
        Task<T?> GetItemAsync<T>(string key);

        /// <summary>
        /// Remove item from IndexedDB
        /// </summary>
        Task RemoveItemAsync(string key);

        /// <summary>
        /// Clear all items from IndexedDB
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Check if key exists in IndexedDB
        /// </summary>
        Task<bool> ContainsKeyAsync(string key);
    }

    /// <summary>
    /// IndexedDB service implementation using JavaScript interop
    /// Provides persistent storage for web app similar to mobile MonkeyCache
    /// </summary>
    public class IndexedDbService : IIndexedDbService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly string _dbName = "ToxiqWebApp";
        private readonly string _storeName = "ToxiqStore";

        public IndexedDbService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Store item in IndexedDB with JSON serialization
        /// </summary>
        public async Task SetItemAsync<T>(string key, T value)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                await _jsRuntime.InvokeVoidAsync("toxiqIndexedDb.setItem", key, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing item in IndexedDB: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get item from IndexedDB with JSON deserialization
        /// </summary>
        public async Task<T?> GetItemAsync<T>(string key)
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("toxiqIndexedDb.getItem", key);

                if (string.IsNullOrEmpty(json))
                {
                    return default(T);
                }

                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving item from IndexedDB: {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Remove item from IndexedDB
        /// </summary>
        public async Task RemoveItemAsync(string key)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("toxiqIndexedDb.removeItem", key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing item from IndexedDB: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Clear all items from IndexedDB
        /// </summary>
        public async Task ClearAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("toxiqIndexedDb.clear");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing IndexedDB: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Check if key exists in IndexedDB
        /// </summary>
        public async Task<bool> ContainsKeyAsync(string key)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("toxiqIndexedDb.containsKey", key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking key in IndexedDB: {ex.Message}");
                return false;
            }
        }
    }
}