using MovieHub.Data.Dtos.Auth;

namespace MovieHub.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task LogoutAsync(string token);
}