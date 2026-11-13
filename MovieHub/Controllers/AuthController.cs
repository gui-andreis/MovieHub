using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Dtos;
using MovieHub.Services;
using MovieHub.Services.Auth;

namespace MovieHub.Controllers;

[Route("api/auth")]
public class AuthController : AppControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentProfile _current;

    public AuthController(IAuthService auth, ICurrentProfile current)
    {
        _auth = auth;
        _current = current;
    }

    /// <summary>Cria a conta (com um perfil inicial) e já retorna os tokens.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
        => Created(await _auth.RegisterAsync(request, ct), "Conta criada com sucesso.");

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginAsync(request, ct), "Login realizado com sucesso.");

    /// <summary>Troca um refresh token válido por um novo par de tokens (rotação).</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(request.RefreshToken, ct), "Tokens renovados.");

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
        await _auth.LogoutAsync(_current.RequireUserId(), token, ct);
        return Done("Logout realizado com sucesso.");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
        => Ok(await _auth.GetMeAsync(_current.RequireUserId()));
}
