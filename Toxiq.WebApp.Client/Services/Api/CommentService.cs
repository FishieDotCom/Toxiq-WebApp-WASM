// Toxiq.WebApp.Client/Services/Api/CommentService.cs
using System.Net.Http.Json;
using Toxiq.Mobile.Dto;

namespace Toxiq.WebApp.Client.Services.Api
{
    /// <summary>
    /// Comment service interface - mirrors mobile app's comment functionality exactly
    /// Reference: Toxiq.Mobile/Service/OnlineDataService/CommentService
    /// </summary>
    public interface ICommentService
    {
        /// <summary>
        /// Get comments for a post
        /// Reference: Mobile app's GetPostComments method
        /// </summary>
        Task<SearchResultDto<Comment>> GetPostComments(GetCommentDto request);

        /// <summary>
        /// Get a specific comment by ID
        /// Reference: Mobile app's GetComment method
        /// </summary>
        Task<Comment?> GetComment(Guid commentId);

        /// <summary>
        /// Submit a comment on a post
        /// Reference: Mobile app's CommentOnPost method
        /// </summary>
        Task<Comment> CommentOnPost(Comment comment);

        /// <summary>
        /// Upvote a comment
        /// Reference: Mobile app's comment voting system
        /// </summary>
        Task<bool> UpvoteComment(Guid commentId);

        /// <summary>
        /// Downvote a comment
        /// Reference: Mobile app's comment voting system
        /// </summary>
        Task<bool> DownvoteComment(Guid commentId);

        /// <summary>
        /// Remove vote from a comment
        /// Reference: Mobile app's delete vote functionality
        /// </summary>
        Task<bool> RemoveCommentVote(Guid commentId);

        /// <summary>
        /// Get available stickers for comments
        /// Reference: Mobile app's GetSticker method
        /// </summary>
        Task<StickerPack> GetSticker();

        /// <summary>
        /// Delete a comment (if user owns it)
        /// Reference: Mobile app's delete comment functionality
        /// </summary>
        Task<bool> DeleteComment(Guid commentId);

        /// <summary>
        /// Report a comment
        /// Reference: Mobile app's reporting system
        /// </summary>
        Task<bool> ReportComment(Guid commentId, string reason);

        /// <summary>
        /// Get comment replies
        /// Reference: Mobile app's reply loading system
        /// </summary>
        Task<SearchResultDto<Comment>> GetCommentReplies(GetCommentDto request);
    }
    /// <summary>
    /// Comment service implementation - mirrors mobile app's CommentService exactly
    /// Reference: Toxiq.Mobile/Service/OnlineDataService/CommentService
    /// </summary>
    public class CommentService : ICommentService
    {
        private readonly OptimizedApiService _api;

        //public CommentService(OptimizedApiService api) => _api = api;

        //private readonly HttpClient _httpClient;
        //private readonly IAuthenticationService _authService;

        public CommentService(OptimizedApiService api)
        {
            //_httpClient = httpClient;
            _api = api;
        }

        /// <summary>
        /// Get comments for a post - mirrors mobile app exactly
        /// Reference: Mobile CommentService.GetPostComments
        /// </summary>
        public async Task<SearchResultDto<Comment>> GetPostComments(GetCommentDto request)
        {
            try

            {
                // Use same endpoint as mobile app
                var response = await _api.PostRawAsync("Comment/GetComments", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SearchResultDto<Comment>>();
                    return result ?? new SearchResultDto<Comment> { Data = new List<Comment>() };
                }

                return new SearchResultDto<Comment> { Data = new List<Comment>() };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting post comments: {ex.Message}");
                return new SearchResultDto<Comment> { Data = new List<Comment>() };
            }
        }

        /// <summary>
        /// Get a specific comment by ID
        /// Reference: Mobile CommentService.GetComment
        /// </summary>
        public async Task<Comment?> GetComment(Guid commentId)
        {
            try
            {
                var response = await _api.GetRawAsync($"Comment/GetComment/{commentId}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Comment>();
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting comment: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Submit a comment on a post - mirrors mobile app submission
        /// Reference: Mobile CommentService.CommentOnPost
        /// </summary>
        public async Task<Comment> CommentOnPost(Comment comment)
        {
            try
            {
                var response = await _api.PostRawAsync("Comment/MakeComment", comment);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Comment>();
                    return result ?? comment;
                }

                throw new Exception("Failed to submit comment");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting comment: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Upvote a comment - same logic as mobile app
        /// Reference: Mobile post voting system adapted for comments
        /// </summary>
        public async Task<bool> UpvoteComment(Guid commentId)
        {
            try
            {
                var voteDto = new CommentVoteDto
                {
                    CommentId = commentId,
                    IsSupport = true
                };

                var response = await _api.PostRawAsync("Comment/Upvote", voteDto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error upvoting comment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downvote a comment - same logic as mobile app
        /// Reference: Mobile post voting system adapted for comments
        /// </summary>
        public async Task<bool> DownvoteComment(Guid commentId)
        {
            try
            {
                var voteDto = new CommentVoteDto
                {
                    CommentId = commentId,
                    IsSupport = false
                };

                var response = await _api.PostRawAsync("Comment/Downvote", voteDto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downvoting comment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove vote from a comment
        /// Reference: Mobile app's delete vote functionality
        /// </summary>
        public async Task<bool> RemoveCommentVote(Guid commentId)
        {
            try
            {
                var response = await _api.GetRawAsync($"Comment/Deletevote/{commentId}");
                //return response.IsSuccessStatusCode;
                return false; // Placeholder for actual delete logic

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing comment vote: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get available stickers - mirrors mobile app exactly
        /// Reference: Mobile CommentService.GetSticker
        /// </summary>
        public async Task<StickerPack> GetSticker()
        {
            try
            {
                var response = await _api.GetRawAsync("Sticker/GetPack/pepo");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<StickerPack>();

                    result.Stickers.ForEach(sticker =>
                    {
                        // Ensure each sticker has a valid MediaType
                        sticker.MediaPath = sticker.MediaPath.Replace("https://api.toxiq.xyz/images/", "https://toxiq.xyz/images/");
                    });

                    return result ?? new StickerPack { Stickers = new List<MediaDto>() };
                }
                return new StickerPack { Stickers = new List<MediaDto>() };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stickers: {ex.Message}");
                return new StickerPack { Stickers = new List<MediaDto>() };
            }
        }

        /// <summary>
        /// Delete a comment (if user owns it)
        /// Reference: Mobile app's delete functionality
        /// </summary>
        public async Task<bool> DeleteComment(Guid commentId)
        {
            try
            {
                //var response = await _httpClient.DeleteAsync($"api/Comment/DeleteComment/{commentId}");
                //return response.IsSuccessStatusCode;
                return false; // Placeholder for actual delete logic
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting comment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Report a comment
        /// Reference: Mobile app's reporting system
        /// </summary>
        public async Task<bool> ReportComment(Guid commentId, string reason)
        {
            try
            {
                var reportDto = new ReportDto
                {
                    ContentId = commentId,
                    ContentType = "Comment",
                    Reason = reason
                };

                var response = await _api.PostRawAsync("Report/ReportContent", reportDto);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reporting comment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get comment replies
        /// Reference: Mobile app's reply loading system
        /// </summary>
        public async Task<SearchResultDto<Comment>> GetCommentReplies(GetCommentDto request)
        {
            try
            {
                // Set IsReply to true for replies
                request.IsReply = true;

                var response = await _api.PostRawAsync("Comment/GetPostComments", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SearchResultDto<Comment>>();
                    return result ?? new SearchResultDto<Comment> { Data = new List<Comment>() };
                }

                return new SearchResultDto<Comment> { Data = new List<Comment>() };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting comment replies: {ex.Message}");
                return new SearchResultDto<Comment> { Data = new List<Comment>() };
            }
        }
    }

    /// <summary>
    /// Comment vote DTO - mirrors mobile app voting structure
    /// </summary>
    public class CommentVoteDto
    {
        public Guid CommentId { get; set; }
        public bool IsSupport { get; set; }
    }

    /// <summary>
    /// Report DTO for content reporting
    /// </summary>
    public class ReportDto
    {
        public Guid ContentId { get; set; }
        public string ContentType { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}