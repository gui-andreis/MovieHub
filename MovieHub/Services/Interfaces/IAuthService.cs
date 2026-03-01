using MovieHub.Data.Dtos.Auth;

namespace MovieHub.Services.Interfaces;

public interface IAuthService
{
    // Registra um novo usuário e retorna dados de autenticação
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);

    // Autentica usuário e retorna token/dados de login
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
}