using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MovieHub.Data.Dtos.Auth;
using MovieHub.Exceptions;
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
    private readonly ITokenBlacklistService _blacklist;

    public AuthService(UserManager<ApplicationUser> userManager, IConfiguration configuration, ITokenBlacklistService blacklist)
    {
        _userManager = userManager;
        _configuration = configuration;
        _blacklist = blacklist;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new BadRequestException(errors);
        }

        await _userManager.AddToRoleAsync(user, "User");
        return await GenerateTokenAsync(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            throw new UnauthorizedException("Email ou senha inválidos.");

        return await GenerateTokenAsync(user);
    }

    public async Task LogoutAsync(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expiresIn = jwt.ValidTo - DateTime.UtcNow;

        if (expiresIn > TimeSpan.Zero)
            await _blacklist.InvalidateTokenAsync(jwt.Id, expiresIn);
    }

    private async Task<AuthResponseDto> GenerateTokenAsync(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Cada role vira uma claim — permite uso de [Authorize(Roles = "Admin")]
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