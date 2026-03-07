using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Common;
using MovieHub.Data.Dtos.Auth;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : AppControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        return Ok(result, "Usuário registrado com sucesso.");
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        return Ok(result, "Login realizado com sucesso.");
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Logout()
    {
        var token = HttpContext.Request.Headers["Authorization"]
            .ToString()
            .Split(" ")
            .Last();
        await _authService.LogoutAsync(token);
        return NoContent("Logout realizado com sucesso.");
    }
}