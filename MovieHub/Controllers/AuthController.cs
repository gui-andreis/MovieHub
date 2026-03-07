using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Data.Dtos.Auth;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : AppControllerBase
{
    private readonly IAuthService _authService;

    // Injeção de dependência do serviço responsável pela lógica de autenticação
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    //Retorna dados do usuário após criação bem-sucedida
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        return Ok(result, "Usuário registrado com sucesso.");
    }

    // Endpoint responsável pela autenticação do usuário
    // Retorna JWT válido se credenciais estiverem corretas
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        return Ok(result, "Login realizado com sucesso.");
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Extrai o token cru do header Authorization (remove o prefixo "Bearer ")
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        await _authService.LogoutAsync(token);
        return NoContent("Logout realizado com sucesso.");
    }
}
