using Blazored.LocalStorage;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Toxiq.Mobile.Dto;

namespace Toxiq.WebApp.Client.Services.Core
{

    /// <summary>
    /// Service for managing component state across the application
    /// Mirrors mobile app's state management patterns
    /// </summary>
    public interface IComponentStateService
    {
        // Collection view state management
        Task<ComponentState<T>> GetCollectionStateAsync<T>(string key) where T : class;
        Task SaveCollectionStateAsync<T>(string key, ComponentState<T> state) where T : class;
        Task ClearCollectionStateAsync(string key);

        // Feed state management (mirrors mobile feed state)
        Task<FeedState> GetFeedStateAsync();
        Task SaveFeedStateAsync(FeedState state);
        Task ClearFeedStateAsync();

        // User state management
        Task<UserState> GetUserStateAsync();
        Task SaveUserStateAsync(UserState state);

        // Events for state changes
        event EventHandler<StateChangedEventArgs>? StateChanged;
    }

    public class ComponentState<T> where T : class
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; } = 0;
        public bool HasMoreItems { get; set; } = true;
        public bool IsLoading { get; set; } = false;
        public double ScrollPosition { get; set; } = 0;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class FeedState
    {
        public ComponentState<BasePost> Posts { get; set; } = new();
        public GetPostDto LastFilter { get; set; } = new();
        public bool IsRefreshing { get; set; } = false;
    }

    public class UserState
    {
        public UserProfile? CurrentUser { get; set; }
        public bool IsAuthenticated { get; set; } = false;
        public Dictionary<string, object> Preferences { get; set; } = new();
    }

    public class StateChangedEventArgs : EventArgs
    {
        public string Key { get; set; } = "";
        public Type StateType { get; set; } = typeof(object);
        public object? NewState { get; set; }
    }


    public class ComponentStateService : IComponentStateService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<ComponentStateService> _logger;

        public event EventHandler<StateChangedEventArgs>? StateChanged;

        private const string COLLECTION_STATE_PREFIX = "collection_state_";
        private const string FEED_STATE_KEY = "feed_state";
        private const string USER_STATE_KEY = "user_state";

        public ComponentStateService(
            IMemoryCache memoryCache,
            ILocalStorageService localStorage,
            ILogger<ComponentStateService> logger)
        {
            _memoryCache = memoryCache;
            _localStorage = localStorage;
            _logger = logger;
        }

        public async Task<ComponentState<T>> GetCollectionStateAsync<T>(string key) where T : class
        {
            try
            {
                var cacheKey = $"{COLLECTION_STATE_PREFIX}{key}";

                // Try memory cache first (faster)
                if (_memoryCache.TryGetValue(cacheKey, out ComponentState<T>? cachedState) && cachedState != null)
                {
                    return cachedState;
                }

                // Try local storage (persistent)
                var localStorageKey = $"toxiq_{cacheKey}";
                if (await _localStorage.ContainKeyAsync(localStorageKey))
                {
                    var serializedState = await _localStorage.GetItemAsStringAsync(localStorageKey);
                    if (!string.IsNullOrEmpty(serializedState))
                    {
                        var state = JsonSerializer.Deserialize<ComponentState<T>>(serializedState);
                        if (state != null)
                        {
                            // Cache in memory for faster access
                            _memoryCache.Set(cacheKey, state, TimeSpan.FromMinutes(30));
                            return state;
                        }
                    }
                }

                // Return new state if none found
                return new ComponentState<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collection state for key: {Key}", key);
                return new ComponentState<T>();
            }
        }

        public async Task SaveCollectionStateAsync<T>(string key, ComponentState<T> state) where T : class
        {
            try
            {
                var cacheKey = $"{COLLECTION_STATE_PREFIX}{key}";

                // Update memory cache
                _memoryCache.Set(cacheKey, state, TimeSpan.FromMinutes(30));

                // Persist to local storage
                var localStorageKey = $"toxiq_{cacheKey}";
                var serializedState = JsonSerializer.Serialize(state);
                await _localStorage.SetItemAsStringAsync(localStorageKey, serializedState);

                // Notify listeners
                StateChanged?.Invoke(this, new StateChangedEventArgs
                {
                    Key = key,
                    StateType = typeof(ComponentState<T>),
                    NewState = state
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving collection state for key: {Key}", key);
            }
        }

        public async Task ClearCollectionStateAsync(string key)
        {
            try
            {
                var cacheKey = $"{COLLECTION_STATE_PREFIX}{key}";

                // Clear memory cache
                _memoryCache.Remove(cacheKey);

                // Clear local storage
                var localStorageKey = $"toxiq_{cacheKey}";
                await _localStorage.RemoveItemAsync(localStorageKey);

                _logger.LogDebug("Cleared collection state for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing collection state for key: {Key}", key);
            }
        }

        public async Task<FeedState> GetFeedStateAsync()
        {
            try
            {
                // Try memory cache first
                if (_memoryCache.TryGetValue(FEED_STATE_KEY, out FeedState? cachedState) && cachedState != null)
                {
                    return cachedState;
                }

                // Try local storage
                var localStorageKey = $"toxiq_{FEED_STATE_KEY}";
                if (await _localStorage.ContainKeyAsync(localStorageKey))
                {
                    var serializedState = await _localStorage.GetItemAsStringAsync(localStorageKey);
                    if (!string.IsNullOrEmpty(serializedState))
                    {
                        var state = JsonSerializer.Deserialize<FeedState>(serializedState);
                        if (state != null)
                        {
                            _memoryCache.Set(FEED_STATE_KEY, state, TimeSpan.FromMinutes(15));
                            return state;
                        }
                    }
                }

                return new FeedState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feed state");
                return new FeedState();
            }
        }

        public async Task SaveFeedStateAsync(FeedState state)
        {
            try
            {
                // Update memory cache
                _memoryCache.Set(FEED_STATE_KEY, state, TimeSpan.FromMinutes(15));

                // Persist to local storage
                var localStorageKey = $"toxiq_{FEED_STATE_KEY}";
                var serializedState = JsonSerializer.Serialize(state);
                await _localStorage.SetItemAsStringAsync(localStorageKey, serializedState);

                StateChanged?.Invoke(this, new StateChangedEventArgs
                {
                    Key = FEED_STATE_KEY,
                    StateType = typeof(FeedState),
                    NewState = state
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving feed state");
            }
        }

        public async Task ClearFeedStateAsync()
        {
            try
            {
                _memoryCache.Remove(FEED_STATE_KEY);

                var localStorageKey = $"toxiq_{FEED_STATE_KEY}";
                await _localStorage.RemoveItemAsync(localStorageKey);

                _logger.LogDebug("Cleared feed state");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing feed state");
            }
        }

        public async Task<UserState> GetUserStateAsync()
        {
            try
            {
                if (_memoryCache.TryGetValue(USER_STATE_KEY, out UserState? cachedState) && cachedState != null)
                {
                    return cachedState;
                }

                var localStorageKey = $"toxiq_{USER_STATE_KEY}";
                if (await _localStorage.ContainKeyAsync(localStorageKey))
                {
                    var serializedState = await _localStorage.GetItemAsStringAsync(localStorageKey);
                    if (!string.IsNullOrEmpty(serializedState))
                    {
                        var state = JsonSerializer.Deserialize<UserState>(serializedState);
                        if (state != null)
                        {
                            _memoryCache.Set(USER_STATE_KEY, state, TimeSpan.FromHours(1));
                            return state;
                        }
                    }
                }

                return new UserState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user state");
                return new UserState();
            }
        }

        public async Task SaveUserStateAsync(UserState state)
        {
            try
            {
                _memoryCache.Set(USER_STATE_KEY, state, TimeSpan.FromHours(1));

                var localStorageKey = $"toxiq_{USER_STATE_KEY}";
                var serializedState = JsonSerializer.Serialize(state);
                await _localStorage.SetItemAsStringAsync(localStorageKey, serializedState);

                StateChanged?.Invoke(this, new StateChangedEventArgs
                {
                    Key = USER_STATE_KEY,
                    StateType = typeof(UserState),
                    NewState = state
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user state");
            }
        }
    }
}

