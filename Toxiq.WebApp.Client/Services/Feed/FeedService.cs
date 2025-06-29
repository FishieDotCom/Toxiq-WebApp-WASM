using Toxiq.Mobile.Dto;

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
        event EventHandler<PostInteractionEventArgs> PostInteractionChanged;
    }

    public class FeedService : IFeedService
    {
        private readonly IApiService _apiService;
        private readonly ILogger<FeedService> _logger;

        public event EventHandler<PostInteractionEventArgs> PostInteractionChanged;

        public FeedService(IApiService apiService, ILogger<FeedService> logger)
        {
            _apiService = apiService;
            _logger = logger;
        }

        public async Task<SearchResultDto<BasePost>> GetFeedAsync(GetPostDto filter)
        {
            try
            {
                var result = await _apiService.PostService.GetFeed(filter);

                // Ensure ReplyType is set for all posts (matching mobile app behavior)
                foreach (var post in result.Data)
                {
                    if (post.ReplyType == null)
                        post.ReplyType = ReplyType.Non;
                }

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

                // Notify listeners of the interaction change
                PostInteractionChanged?.Invoke(this, new PostInteractionEventArgs
                {
                    PostId = postId,
                    NewSupportStatus = true
                });

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

                PostInteractionChanged?.Invoke(this, new PostInteractionEventArgs
                {
                    PostId = postId,
                    NewSupportStatus = false
                });

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
                // Note: API might be Deletevote endpoint based on mobile code
                await _apiService.PostService.Downvote(postId); // Update based on actual endpoint

                PostInteractionChanged?.Invoke(this, new PostInteractionEventArgs
                {
                    PostId = postId,
                    NewSupportStatus = null
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove vote from post {PostId}", postId);
                return false;
            }
        }
    }

    public class PostInteractionEventArgs : EventArgs
    {
        public Guid PostId { get; set; }
        public int? NewSupportCount { get; set; }
        public bool? NewSupportStatus { get; set; }
    }
}
