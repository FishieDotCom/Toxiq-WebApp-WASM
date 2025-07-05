// Toxiq.WebApp.Client/Services/Api/NotesServiceImpl.cs
using Toxiq.Mobile.Dto;

namespace Toxiq.WebApp.Client.Services.Api
{
    public class NotesServiceImpl : INotesService
    {
        private readonly OptimizedApiService _apiService;

        public NotesServiceImpl(OptimizedApiService apiService)
        {
            _apiService = apiService;

        }

        public async ValueTask<List<NoteDto>> GetMyNotes()
        {
            try
            {
                const string cacheKey = "my_notes";

                // Check cache first
                //var cached = await _cache.GetAsync<List<NoteDto>>(cacheKey);
                //if (cached != null)
                //{
                //    return cached;
                //}

                var result = await _apiService.GetAsync<List<NoteDto>>("Notes/GetMyNotes");

                if (result != null)
                {
                    // Cache notes for a short time
                    //await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
                    return result;
                }

                return new List<NoteDto>();
            }
            catch (Exception ex)
            {
                return new List<NoteDto>();
            }
        }

        public async ValueTask<NoteDto> GetNote(Guid id)
        {
            try
            {
                var cacheKey = $"note_{id}";

                // Check cache first
                //var cached = await _cache.GetAsync<NoteDto>(cacheKey);
                //if (cached != null)
                //{
                //    return cached;
                //}

                var result = await _apiService.GetAsync<NoteDto>($"Notes/GetNote/{id}");

                if (result != null)
                {
                    // Cache individual notes for longer
                    //await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1));
                }

                return result ?? throw new InvalidOperationException($"Note with ID {id} not found");
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error getting note with ID: {NoteId}", id);
                throw;
            }
        }

        public async ValueTask<HttpResponseMessage> SendNote(NoteDto input)
        {
            try
            {

                // Validate note input
                if (string.IsNullOrWhiteSpace(input.Content))
                {
                    throw new ArgumentException("Note content cannot be empty");
                }


                var response = await _apiService.PostRawAsync("Notes/SendNote", input);

                if (response.IsSuccessStatusCode)
                {
                    // Invalidate cache for user's notes
                    //await _cache.RemoveAsync("my_notes");
                    //_logger.LogInformation("Note sent successfully");
                }

                return response;
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error sending note: {@Note}", input);
                throw;
            }
        }

        public async ValueTask RespondToNote(BasePost input)
        {
            try
            {
                //_logger.LogInformation("Responding to note with post content: {Content}",
                // input.Content?.Substring(0, Math.Min(50, input.Content?.Length ?? 0)));

                // Validate post input (same as regular post validation)
                if (string.IsNullOrWhiteSpace(input.Content))
                {
                    throw new ArgumentException("Response content cannot be empty");
                }

                if (input.Content.Length > 512)
                {
                    throw new ArgumentException("Response content exceeds maximum length of 512 characters");
                }

                // Set required fields
                input.Type = PostType.Text;
                input.ReplyType = ReplyType.Note;

                if (string.IsNullOrEmpty(input.PostColor))
                {
                    input.PostColor = "#5a189a"; // Default color
                }

                // Send note response (matches mobile app behavior)
                var response = await _apiService.PostAsync<BasePost>("Notes/RespondToNote", input);

                if (response != null)
                {
                    //_logger.LogInformation("Note response published successfully with ID: {PostId}", response.Id);

                    // Invalidate relevant caches
                    //await _cache.RemoveByPatternAsync("feed_*");
                    //await _cache.RemoveAsync("my_notes");
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error responding to note: {@Post}", input);
                throw;
            }
        }
    }
}