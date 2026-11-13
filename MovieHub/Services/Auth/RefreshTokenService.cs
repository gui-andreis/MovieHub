using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieHub.Data;
using MovieHub.Exceptions;
using MovieHub.Models;

namespace MovieHub.Services.Auth;

public interface IRefreshTokenService
{
    /// <summary>Emite um novo refresh token (retorna o valor em claro, que só o cliente guarda).</summary>
    Task<string> IssueAsync(string userId, CancellationToken ct);

    /// <summary>Valida e rotaciona: o token usado é revogado e um novo é emitido. Retorna (userId, novoToken).</summary>
    Task<(string UserId, string NewToken)> RotateAsync(string refreshToken, CancellationToken ct);

    Task RevokeAllAsync(string userId, CancellationToken ct);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(ApplicationDbContext db, IOptions<JwtOptions> options, ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<string> IssueAsync(string userId, CancellationToken ct)
    {
        var token = Generate();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        });

        // Limpa tokens antigos do usuário para a tabela não crescer indefinidamente
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var stale = await _db.RefreshTokens
            .Where(r => r.UserId == userId && (r.ExpiresAt < DateTime.UtcNow || (r.RevokedAt != null && r.RevokedAt < cutoff)))
            .ToListAsync(ct);
        _db.RefreshTokens.RemoveRange(stale);

        await _db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<(string UserId, string NewToken)> RotateAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct)
            ?? throw new UnauthorizedException("Refresh token inválido.");

        if (stored.RevokedAt != null)
        {
            // Reuso de um token já rotacionado: possível vazamento → revoga a família inteira
            _logger.LogWarning("Reuso de refresh token detectado para o usuário {UserId}", stored.UserId);
            await RevokeAllAsync(stored.UserId, ct);
            throw new UnauthorizedException("Refresh token já utilizado. Faça login novamente.");
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token expirado. Faça login novamente.");

        var newToken = Generate();
        var newHash = Hash(newToken);
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        });
        await _db.SaveChangesAsync(ct);

        return (stored.UserId, newToken);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken ct)
    {
        var active = await _db.RefreshTokens.Where(r => r.UserId == userId && r.RevokedAt == null).ToListAsync(ct);
        foreach (var token in active) token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
