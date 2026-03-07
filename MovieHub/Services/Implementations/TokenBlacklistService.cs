using MovieHub.Services.Interfaces;
using StackExchange.Redis;

namespace MovieHub.Services.Implementations;

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IDatabase _redis;

    public TokenBlacklistService(IConnectionMultiplexer multiplexer)
    {
        _redis = multiplexer.GetDatabase();
    }

    // Salva o Jti no Redis com TTL igual ao tempo restante do token
    // Quando o token expiraria de qualquer forma, o Redis remove automaticamente
    public async Task InvalidateTokenAsync(string jti, TimeSpan expiresIn)
    {
        await _redis.StringSetAsync(
            key: $"blacklist:{jti}",
            value: "invalid",
            expiry: expiresIn
        );
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        return await _redis.KeyExistsAsync($"blacklist:{jti}");
    }
}