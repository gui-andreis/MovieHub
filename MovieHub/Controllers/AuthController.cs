using Microsoft.AspNetCore.Mvc;
using MovieHub.Data.Dtos.Auth;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
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

        if (result == null)
            return BadRequest("Erro ao registrar usuário.");

        return Ok(result);
    }

    // Endpoint responsável pela autenticação do usuário
    // Retorna JWT válido se credenciais estiverem corretas
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        // Retorna 401 quando credenciais são inválidas
        if (result == null)
            return Unauthorized("Email ou senha inválidos.");

        return Ok(result);
    }
}