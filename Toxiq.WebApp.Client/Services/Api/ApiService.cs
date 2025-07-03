using Toxiq.Mobile.Dto;
using Toxiq.WebApp.Client.Services.Api;

public interface IApiService
{
    IAuthService AuthService { get; }
    IUserService UserService { get; }
    IPostService PostService { get; }
    ICommentService CommentService { get; }
    INotesService NotesService { get; }
    IColorService ColorService { get; }
}

// Individual service interfaces
public interface IAuthService
{
    ValueTask<bool> CheckHeartBeat();
    ValueTask<LoginResponse> Login(LoginDto loginDto);
}

public interface IUserService
{
    ValueTask<UserProfile> GetMe(bool force = false);
    ValueTask<UserProfile> GetUser(string username);
    ValueTask<bool> CheckUsername(string username);
    ValueTask<bool> ChangeUsername(string username);
    ValueTask EditProfile(UserProfile profile);
    ValueTask<List<BasePost>> GetUserPosts(string username, bool includeReplies = false);
}

public interface IPostService
{
    ValueTask<SearchResultDto<BasePost>> GetFeed(GetPostDto filter);
    ValueTask<BasePost> GetPost(Guid postId);
    ValueTask<BasePost> GetPrompt(Guid postId);
    ValueTask<SearchResultDto<BasePost>> GetPostsByPrompt(Guid promptId, int page, int pageSize);
    ValueTask Publish(BasePost post);
    ValueTask Upvote(Guid id);
    ValueTask Downvote(Guid id);
}

public interface INotesService
{
    ValueTask<List<NoteDto>> GetMyNotes();
    ValueTask<NoteDto> GetNote(Guid id);
    ValueTask<HttpResponseMessage> SendNote(NoteDto input);
    ValueTask RespondToNote(BasePost input);
}

public interface IColorService
{
    ValueTask<List<ColorListDto>> GetColors();
    ValueTask SuggestColor(string hex);
}