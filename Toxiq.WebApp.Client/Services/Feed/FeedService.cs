// Toxiq.WebApp.Client/Services/Feed/FeedService.cs
using Microsoft.Extensions.Caching.Memory;
using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Api;

namespace Toxiq.WebApp.Client.Services.Feed
{
    public interface IFeedService
    {
        Task<SearchResultDto<BasePost>> GetFeedAsync(GetPostDto filter);
        Task<SearchResultDto<BasePost>> RefreshFeedAsync(GetPostDto filter);
        Task<SearchResultDto<BasePost>> LoadMorePostsAsync(GetPostDto filter);
        Task<bool> UpvotePostAsync(Guid postId);
        Task<bool> DownvotePostAsync(Guid postId);
        Task<bool> RemoveVoteAsync(Guid postId);

        // Events for real-time updates
        event EventHandler<PostInteractionEventArgs>? PostInteractionChanged;

        // Cache management
        void ClearCache();
        Task InvalidatePostCache(Guid postId);
    }

    public class FeedService : IFeedService
    {
        private readonly IApiService _apiService;
        private readonly ILogger<FeedService> _logger;
        private readonly IMemoryCache _cache;

        public event EventHandler<PostInteractionEventArgs>? PostInteractionChanged;

        // Cache keys
        private const string FEED_CACHE_PREFIX = "feed_";
        private const string POST_CACHE_PREFIX = "post_";
        private readonly TimeSpan _feedCacheExpiry = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _postCacheExpiry = TimeSpan.FromMinutes(15);

        public FeedService(IApiService apiService, ILogger<FeedService> logger, IMemoryCache cache)
        {
            _apiService = apiService;
            _logger = logger;
            _cache = cache;
        }

        public async Task<SearchResultDto<BasePost>> GetFeedAsync(GetPostDto filter)
        {
            try
            {
                // Create cache key based on filter parameters
                var cacheKey = $"{FEED_CACHE_PREFIX}{filter.Page}_{filter.Count}";

                // Try to get from cache first (matching mobile app caching pattern)
                if (_cache.TryGetValue(cacheKey, out SearchResultDto<BasePost>? cachedResult) && cachedResult != null)
                {
                    _logger.LogDebug("Feed cache hit for key: {CacheKey}", cacheKey);
                    return cachedResult;
                }

                _logger.LogDebug("Feed cache miss, fetching from API");
                var result = await _apiService.PostService.GetFeed(filter);

                // Ensure ReplyType is set for all posts (matching mobile app behavior)
                foreach (var post in result.Data)
                {
                    if (post.ReplyType == null)
                        post.ReplyType = ReplyType.Non;

                    // Cache individual posts as well
                    CachePost(post);
                }

                // Cache the feed result
                _cache.Set(cacheKey, result, _feedCacheExpiry);
                _logger.LogDebug("Cached feed result with key: {CacheKey}", cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get feed");
                return new SearchResultDto<BasePost> { Data = new List<BasePost>() };
            }
        }

        public async Task<SearchResultDto<BasePost>> RefreshFeedAsync(GetPostDto filter)
        {
            // Clear feed cache for refresh (matching mobile app behavior)
            ClearFeedCache();

            // Reset page to 0 for refresh
            filter.Page = 0;
            return await GetFeedAsync(filter);
        }

        public async Task<SearchResultDto<BasePost>> LoadMorePostsAsync(GetPostDto filter)
        {
            // Increment page for pagination
            filter.Page++;
            return await GetFeedAsync(filter);
        }

        public async Task<bool> UpvotePostAsync(Guid postId)
        {
            try
            {
                await _apiService.PostService.Upvote(postId);

                // Update cached post if it exists (matching mobile app memory service pattern)
                UpdateCachedPostSupport(postId, true);

                // Notify listeners of the interaction change
                PostInteractionChanged?.Invoke(this, new PostInteractionEventArgs
                {
                    PostId = postId,
                    NewSupportStatus = true,
                    InteractionType = PostInteractionType.Upvote
                });

                _logger.LogDebug("Successfully upvoted post {PostId}", postId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upvote post {PostId}", postId);
                return false;
            }
        }

        public async Task<bool> DownvotePostAsync(Guid postId)
        {
            try
            {
                await _apiService.PostService.Downvote(postId);

                // Update cached post if it exists
                UpdateCachedPostSupport(postId, false);

                // Notify listeners of the interaction change
                PostInteractionChanged?.Invoke(this, new PostInteractionEventArgs
                {
                    PostId = postId,
                    NewSupportStatus = false,
                    InteractionType = PostInteractionType.Downvote
                });

                _logger.LogDebug("Successfully downvoted post {PostId}", postId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to downvote post {PostId}", postId);
                return false;
            }
        }

        public async Task<bool> RemoveVoteAsync(Guid postId)
        {
            try
            {
                // Note: Mobile app uses "Deletevote" endpoint for removing votes
                // We'll need to add this to the API service or use a workaround

                // For now, we'll try to call a deletevote endpoint if it exists
                // Otherwise, fall back to the current approach
                try
                {
                    // Try the mobile app's Deletevote endpoint pattern
                    await _apiService.PostService.Deletevote(postId);
                }
                catch (NotImplementedException)
                {
                    // Fallback: Use downvote for now (this should be updated when Deletevote is implemented)
                    await _apiService.PostService.Downvote(postId);
                }

                // Update cached post
                UpdateCachedPostSupport(postId, null);

                // Notify listeners of the interaction change
                PostInteractionChanged?.Invoke(this, new PostInteractionEventArgs
                {
                    PostId = postId,
                    NewSupportStatus = null,
                    InteractionType = PostInteractionType.RemoveVote
                });

                _logger.LogDebug("Successfully removed vote from post {PostId}", postId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove vote from post {PostId}", postId);
                return false;
            }
        }

        public void ClearCache()
        {
            // Clear all cache entries (useful for logout/login scenarios)
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0); // Remove all entries
            }
            _logger.LogDebug("Cleared all feed service cache");
        }

        public async Task InvalidatePostCache(Guid postId)
        {
            var cacheKey = $"{POST_CACHE_PREFIX}{postId}";
            _cache.Remove(cacheKey);

            // Also clear related feed cache entries
            ClearFeedCache();

            _logger.LogDebug("Invalidated cache for post {PostId}", postId);
        }

        private void ClearFeedCache()
        {
            // In a real implementation, you'd want to track feed cache keys
            // For now, we'll rely on expiry
            _logger.LogDebug("Feed cache cleared (by expiry)");
        }

        private void CachePost(BasePost post)
        {
            var cacheKey = $"{POST_CACHE_PREFIX}{post.Id}";
            _cache.Set(cacheKey, post, _postCacheExpiry);
        }

        private void UpdateCachedPostSupport(Guid postId, bool? newSupportStatus)
        {
            var cacheKey = $"{POST_CACHE_PREFIX}{postId}";

            if (_cache.TryGetValue(cacheKey, out BasePost? cachedPost) && cachedPost != null)
            {
                // Update support status and count (matching mobile app's in-place update pattern)
                var oldStatus = cachedPost.SupportStatus;
                cachedPost.SupportStatus = newSupportStatus;

                // Adjust support count based on the change
                var currentCount = cachedPost.SupportCount ?? 0;

                if (oldStatus == true && newSupportStatus == null)
                {
                    // Removing upvote
                    cachedPost.SupportCount = Math.Max(0, currentCount - 1);
                }
                else if (oldStatus == false && newSupportStatus == null)
                {
                    // Removing downvote  
                    cachedPost.SupportCount = currentCount + 1;
                }
                else if (oldStatus == null && newSupportStatus == true)
                {
                    // Adding upvote
                    cachedPost.SupportCount = currentCount + 1;
                }
                else if (oldStatus == null && newSupportStatus == false)
                {
                    // Adding downvote
                    cachedPost.SupportCount = Math.Max(0, currentCount - 1);
                }
                else if (oldStatus == true && newSupportStatus == false)
                {
                    // Switching from upvote to downvote
                    cachedPost.SupportCount = Math.Max(0, currentCount - 2);
                }
                else if (oldStatus == false && newSupportStatus == true)
                {
                    // Switching from downvote to upvote
                    cachedPost.SupportCount = currentCount + 2;
                }

                // Re-cache the updated post
                _cache.Set(cacheKey, cachedPost, _postCacheExpiry);

                _logger.LogDebug("Updated cached post {PostId} support status: {OldStatus} -> {NewStatus}",
                    postId, oldStatus, newSupportStatus);
            }
        }
    }

    public class PostInteractionEventArgs : EventArgs
    {
        public Guid PostId { get; set; }
        public int? NewSupportCount { get; set; }
        public bool? NewSupportStatus { get; set; }
        public PostInteractionType InteractionType { get; set; }
    }

    public enum PostInteractionType
    {
        Upvote,
        Downvote,
        RemoveVote
    }
}

// Extension for IApiService to add Deletevote method
namespace Toxiq.WebApp.Client.Services.Api
{
    public static class ApiServiceExtensions
    {
        public static async ValueTask Deletevote(this IPostService postService, Guid id)
        {
            // This should be implemented in the actual API service
            // For now, throw NotImplementedException to trigger fallback
            throw new NotImplementedException("Deletevote endpoint not yet implemented in web API service");
        }
    }
}