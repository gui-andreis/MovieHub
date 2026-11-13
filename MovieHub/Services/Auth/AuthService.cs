using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;

namespace MovieHub.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct);
    Task LogoutAsync(string userId, string? accessToken, CancellationToken ct);
    Task<UserDto> GetMeAsync(string userId);
}

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ITokenBlacklistService _blacklist;
    private readonly JwtOptions _jwt;

    public AuthService(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        IRefreshTokenService refreshTokens,
        ITokenBlacklistService blacklist,
        IOptions<JwtOptions> jwt)
    {
        _users = users;
        _db = db;
        _refreshTokens = refreshTokens;
        _blacklist = blacklist;
        _jwt = jwt.Value;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await _users.FindByEmailAsync(email) != null)
            throw new ConflictException("Já existe uma conta com este email.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Name = request.Name.Trim()
        };

        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await _users.AddToRoleAsync(user, "User");

        // Toda conta nasce com um perfil principal
        _db.Profiles.Add(new Profile
        {
            UserId = user.Id,
            Name = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.Name,
            AvatarKey = "avatar-01"
        });
        await _db.SaveChangesAsync(ct);

        return await BuildResultAsync(user, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user == null || !await _users.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedException("Email ou senha inválidos.");

        return await BuildResultAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var (userId, newRefresh) = await _refreshTokens.RotateAsync(refreshToken, ct);
        var user = await _users.FindByIdAsync(userId)
            ?? throw new UnauthorizedException("Usuário não encontrado.");

        var (access, expiresAt) = await CreateAccessTokenAsync(user);
        return new AuthResult(access, newRefresh, expiresAt, await ToUserDtoAsync(user));
    }

    public async Task LogoutAsync(string userId, string? accessToken, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(accessToken))
            {
                var jwt = handler.ReadJwtToken(accessToken);
                var remaining = jwt.ValidTo - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                    await _blacklist.InvalidateTokenAsync(jwt.Id, remaining);
            }
        }

        await _refreshTokens.RevokeAllAsync(userId, ct);
    }

    public async Task<UserDto> GetMeAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("Usuário não encontrado.");
        return await ToUserDtoAsync(user);
    }

    private async Task<AuthResult> BuildResultAsync(ApplicationUser user, CancellationToken ct)
    {
        var (access, expiresAt) = await CreateAccessTokenAsync(user);
        var refresh = await _refreshTokens.IssueAsync(user.Id, ct);
        return new AuthResult(access, refresh, expiresAt, await ToUserDtoAsync(user));
    }

    private async Task<UserDto> ToUserDtoAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        return new UserDto(user.Id, user.Name, user.Email!, roles.ToList());
    }

    private async Task<(string Token, DateTime ExpiresAt)> CreateAccessTokenAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
