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
    ValueTask<LoginResponse> TG_Login(LoginDto loginDto);
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