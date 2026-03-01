using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MovieHub.Data.Dtos.Auth;
using MovieHub.Models;
using MovieHub.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MovieHub.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    // Injeta UserManager (ASP.NET Identity) para gerenciar usuários
    // IConfiguration é usada para acessar configurações do JWT
    public AuthService(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    // Responsável por registrar novo usuário no sistema
    // Retorna token JWT automaticamente após criação bem-sucedida
    public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        // Caso falhe (ex: senha fraca, email duplicado), retorna null
        if (!result.Succeeded)
            return null;

        await _userManager.AddToRoleAsync(user, "User");

        return await GenerateTokenAsync(user);
    }

    // Realiza autenticação verificando email e senha
    // Retorna token JWT válido se credenciais estiverem corretas
    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return null;

        var validPassword = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!validPassword)
            return null;

        return await GenerateTokenAsync(user);
    }

    //  Gera o JWT contendo identificação e permissões do usuário
    private async Task<AuthResponseDto> GenerateTokenAsync(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");

        // Recupera roles associadas ao usuário
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),// Identificador único do usuário
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Cada role vira uma claim do tipo Role
        // Permite uso de [Authorize(Roles = "Admin")] nos controllers
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)
        );
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(
            double.Parse(jwtSettings["ExpirationInMinutes"]!)
        );

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: creds
        );

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expiration = expiration,
            Email = user.Email!
        };
    }
}