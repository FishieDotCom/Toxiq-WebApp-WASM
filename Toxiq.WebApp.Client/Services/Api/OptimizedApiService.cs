// Toxiq.WebApp.Client/Services/Api/OptimizedApiService.cs
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Toxiq.Mobile.Dto;
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

        // Service implementations
        public IAuthService AuthService { get; }
        public IUserService UserService { get; }
        public IPostService PostService { get; }
        public ICommentService CommentService { get; }
        public INotesService NotesService { get; }
        public IColorService ColorService { get; }

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

            // Initialize service implementations
            AuthService = new AuthServiceImpl(this);
            UserService = new UserServiceImpl(this);
            PostService = new PostServiceImpl(this);
            CommentService = new CommentService(this);
            NotesService = new NotesServiceImpl(this);
            ColorService = new ColorServiceImpl(this);
        }

        // Internal methods for service implementations
        internal async ValueTask<T> GetAsync<T>(string endpoint)
        {
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

        internal async ValueTask<T> GetCachedAsync<T>(string endpoint, TimeSpan maxAge)
        {
            var cacheKey = $"api_{endpoint}";

            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                return await GetAsync<T>(endpoint);
            }, maxAge);
        }

        internal async ValueTask<T> PostAsync<T>(string endpoint, object data)
        {
            await EnsureAuthenticatedAsync();

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseJson, JsonOptions);
        }

        internal async ValueTask<HttpResponseMessage> GetRawAsync(string endpoint)
        {
            await EnsureAuthenticatedAsync();
            return await _httpClient.GetAsync(endpoint);
        }

        internal async ValueTask<HttpResponseMessage> PostRawAsync(string endpoint, object data)
        {
            await EnsureAuthenticatedAsync();

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _httpClient.PostAsync(endpoint, content);
        }

        private async ValueTask EnsureAuthenticatedAsync()
        {
            var token = await _tokenStorage.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    // Service implementations
    internal class AuthServiceImpl : IAuthService
    {
        private readonly OptimizedApiService _api;

        public AuthServiceImpl(OptimizedApiService api) => _api = api;

        public async ValueTask<bool> CheckHeartBeat()
        {
            try
            {
                await _api.GetAsync<object>("Auth/Ping");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ValueTask<LoginResponse> Login(LoginDto loginDto) =>
            _api.PostAsync<LoginResponse>("Auth/DebugLogin", loginDto);

    }

    internal class UserServiceImpl : IUserService
    {
        private readonly OptimizedApiService _api;

        public UserServiceImpl(OptimizedApiService api) => _api = api;

        public ValueTask<UserProfile> GetMe(bool force = false) =>
            force ? _api.GetAsync<UserProfile>("User/GetMe")
                  : _api.GetCachedAsync<UserProfile>("User/GetMe", TimeSpan.FromMinutes(30));

        public ValueTask<UserProfile> GetUser(string username) =>
            _api.GetCachedAsync<UserProfile>($"User/GetUser/{username}", TimeSpan.FromMinutes(15));

        public async ValueTask<bool> CheckUsername(string username)
        {
            try
            {
                await _api.GetAsync<object>($"User/CheckUsername?username={username}");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask<bool> ChangeUsername(string username)
        {
            try
            {
                await _api.GetAsync<object>($"User/ChangeUsername?username={username}");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async ValueTask EditProfile(UserProfile profile) =>
          await _api.PostAsync<object>("User/EditUserProfile", profile);

        public ValueTask<List<BasePost>> GetUserPosts(string username, bool includeReplies = false) =>
            _api.GetCachedAsync<List<BasePost>>($"User/GetUserPosts/{username}", TimeSpan.FromMinutes(5));
    }

    internal class PostServiceImpl : IPostService
    {
        private readonly OptimizedApiService _api;

        public PostServiceImpl(OptimizedApiService api) => _api = api;

        public ValueTask<SearchResultDto<BasePost>> GetFeed(GetPostDto filter) =>
            _api.PostAsync<SearchResultDto<BasePost>>("Post/Feed", filter);

        public ValueTask<BasePost> GetPost(Guid postId) =>
            _api.GetCachedAsync<BasePost>($"Post/GetPost/{postId}", TimeSpan.FromMinutes(10));

        public ValueTask<BasePost> GetPrompt(Guid postId) =>
            _api.GetAsync<BasePost>($"Post/GetPrompt/{postId}");

        public ValueTask<SearchResultDto<BasePost>> GetPostsByPrompt(Guid promptId, int page, int pageSize) =>
            _api.GetAsync<SearchResultDto<BasePost>>($"Post/GetPostsByPrompt/{promptId}?page={page}&pageSize={pageSize}");

        public async ValueTask Publish(BasePost post) =>
          await _api.PostAsync<object>("Post/Publish", post);

        public async ValueTask Upvote(Guid id) =>
           await _api.GetRawAsync($"Post/Upvote/{id}");

        public async ValueTask Downvote(Guid id) =>
           await _api.GetRawAsync($"Post/Downvote/{id}");
    }

    internal class NotesServiceImpl : INotesService
    {
        private readonly OptimizedApiService _api;

        public NotesServiceImpl(OptimizedApiService api) => _api = api;

        public ValueTask<List<NoteDto>> GetMyNotes() =>
            _api.GetAsync<List<NoteDto>>("Notes/GetMyNotes");

        public ValueTask<NoteDto> GetNote(Guid id) =>
            _api.GetAsync<NoteDto>($"Notes/GetNote/{id}");

        public ValueTask<HttpResponseMessage> SendNote(NoteDto input) =>
            _api.PostRawAsync("Notes/SendNote", input);

        public async ValueTask RespondToNote(BasePost input) =>
          await _api.PostAsync<object>("Notes/RespondToNote", input);
    }

    internal class ColorServiceImpl : IColorService
    {
        private readonly OptimizedApiService _api;

        public ColorServiceImpl(OptimizedApiService api) => _api = api;

        public ValueTask<List<ColorListDto>> GetColors() =>
            _api.GetCachedAsync<List<ColorListDto>>("Color/DebugColorList", TimeSpan.FromDays(1));

        public async ValueTask SuggestColor(string hex) =>
           await _api.GetAsync<object>($"Color/SuggestColor/{hex}");
    }
}