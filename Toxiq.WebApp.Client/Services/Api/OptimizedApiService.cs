﻿// Toxiq.WebApp.Client/Services/Api/OptimizedApiService.cs
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
            UserService = new UserService(this, cache);
            PostService = new PostService(this, cache);
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

        internal async ValueTask<T> PutAsync<T>(string endpoint, object data)
        {
            await EnsureAuthenticatedAsync();

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);
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


        internal async Task<HttpResponseMessage> DeleteAsync(string endpoint)
        {
            await EnsureAuthenticatedAsync();


            var response = await _httpClient.DeleteAsync(endpoint);
            response.EnsureSuccessStatusCode();

            return response;
        }


        private async ValueTask EnsureAuthenticatedAsync()
        {
            var token = await _tokenStorage.GetAccessTokenAsync();
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

        public ValueTask<LoginResponse> TG_Login(LoginDto loginDto) =>
            _api.PostAsync<LoginResponse>("Auth/TG_WEB_LOGIN", loginDto);

    }



    internal class ColorServiceImpl : IColorService
    {
        private readonly OptimizedApiService _api;

        public ColorServiceImpl(OptimizedApiService api) => _api = api;

        public ValueTask<List<ColorListDto>> GetColors() =>
            _api.GetAsync<List<ColorListDto>>("Color/DebugColorList");

        public async ValueTask SuggestColor(string hex) =>
           await _api.GetAsync<object>($"Color/SuggestColor/{hex}");
    }
}