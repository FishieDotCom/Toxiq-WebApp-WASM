using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Caching;

namespace Toxiq.WebApp.Client.Services.Api
{
    public interface IPostService
    {
        Task<SearchResultDto<BasePost>> GetFeed(GetPostDto filter);
        Task<BasePost> GetPost(Guid id, bool forceRefresh = false);
        Task<BasePost> GetPrompt(Guid postId);
        Task Publish(BasePost post);
        Task Upvote(Guid id);
        Task Downvote(Guid id);
        Task Deletevote(Guid id);

        Task<SearchResultDto<BasePost>> GetPostsByPrompt(Guid promptId, int page = 1, int count = 10);
    }
    public class PostService : IPostService
    {
        private readonly ICacheService _cache; // IndexedDB
        private readonly OptimizedApiService _api;

        public PostService(OptimizedApiService apiService, ICacheService cacheService)
        {
            _api = apiService;
            _cache = cacheService;
        }

        public async Task<SearchResultDto<BasePost>> GetFeed(GetPostDto filter)
        {
            var results = await _api.PostAsync<SearchResultDto<BasePost>>("Post/Feed", filter);

            if (results == null)
                return null;

            return results;
        }

        public async Task<BasePost> GetPost(Guid id, bool forceRefresh = false)
        {
            string Key = $"post-{id}";
            BasePost cachedPost = null;

            // Always check memory first for UI responsiveness
            if (!forceRefresh)
            {
                cachedPost = await _cache.GetAsync<BasePost>(Key);
                if (cachedPost != null)
                {
                    return cachedPost;
                }
            }


            if (cachedPost == null)
            {
                return await _cache.GetOrCreateAsync(Key, async () =>
                {
                    return await _api.GetAsync<BasePost>($"Post/GetPost/{id}");
                }, TimeSpan.FromMinutes(5));
            }

            // Return cached post if we have one but couldn't get fresh data
            return cachedPost;
        }

        public async Task Publish(BasePost post)
        {
            var publishedPost = await _api.PostAsync<BasePost>("Post/Publish", post);
        }

        public async Task<BasePost> GetPrompt(Guid postId)
        {
            NetworkAccess accessType = Connectivity.Current.NetworkAccess;
            string Key = $"prompt-{postId}";

            return await _cache.GetOrCreateAsync(Key, async () =>
            {
                return await _api.GetAsync<BasePost>($"Post/GetPrompt/{postId}");
            }, TimeSpan.FromDays(30));
        }


        public async Task Upvote(Guid id)
        {
            var response = await _api.GetRawAsync($"Post/Upvote/{id}");

        }

        public async Task Downvote(Guid id)
        {
            var response = await _api.GetRawAsync($"Post/Downvote/{id}");

        }
        public async Task Deletevote(Guid id)
        {
            var response = await _api.GetRawAsync($"Post/Deletevote/{id}");
        }

        public async Task<SearchResultDto<BasePost>> GetPostsByPrompt(Guid promptId, int page = 1, int count = 10)
        {
            return await _api.GetAsync<SearchResultDto<BasePost>>($"Post/GetPostsByPrompt/{promptId}?page={page}&count={count}");
        }
    }
}
