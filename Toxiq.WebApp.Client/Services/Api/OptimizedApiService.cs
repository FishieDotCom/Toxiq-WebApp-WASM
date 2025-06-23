using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Toxiq.WebApp.Client.Services.Authentication;
using Toxiq.WebApp.Client.Services.Caching;

namespace Toxiq.WebApp.Client.Services.Api
{
    public class OptimizedApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cache;
        private readonly ITokenStorage _tokenStorage;
        private readonly ILogger<OptimizedApiService> _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _requestSemaphores = new();

        public OptimizedApiService(
            HttpClient httpClient,
            ICacheService cache,
            ITokenStorage tokenStorage,
            ILogger<OptimizedApiService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _tokenStorage = tokenStorage;
            _logger = logger;

            // Configure HTTP client
            _httpClient.BaseAddress = new Uri("https://toxiq.xyz/api/");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Toxiq-WebApp/1.0");
        }

        public async ValueTask<T> GetCachedAsync<T>(string endpoint, TimeSpan maxAge)
        {
            var cacheKey = $"api_{endpoint}";

            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                await EnsureAuthenticatedAsync();

                using var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }, maxAge);
        }

        public async ValueTask<T> GetAsync<T>(string endpoint)
        {
            // Request deduplication
            var semaphore = _requestSemaphores.GetOrAdd(endpoint, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                await EnsureAuthenticatedAsync();

                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async ValueTask<T> PostAsync<T>(string endpoint, object data)
        {
            await EnsureAuthenticatedAsync();

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseJson, JsonOptions);
        }

        private async ValueTask EnsureAuthenticatedAsync()
        {
            var token = await _tokenStorage.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Implement specific service interfaces
        public IAuthService AuthService { get; }
        public IUserService UserService { get; }
        public IPostService PostService { get; }
        public ICommentService CommentService { get; }
        public INotesService NotesService { get; }
        public IColorService ColorService { get; }
    }
}
