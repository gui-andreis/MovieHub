using MovieHub.Data.Dtos.Auth;

namespace MovieHub.Services.Interfaces;

public interface IAuthService
{
    // Registra um novo usuário e retorna dados de autenticação
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task LogoutAsync(string token);
}