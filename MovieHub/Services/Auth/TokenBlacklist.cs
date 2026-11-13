using System.Collections.Concurrent;
using StackExchange.Redis;

namespace MovieHub.Services.Auth;

/// <summary>Lista de access tokens invalidados (logout) até expirarem.</summary>
public interface ITokenBlacklistService
{
    Task InvalidateTokenAsync(string jti, TimeSpan expiresIn);
    Task<bool> IsTokenBlacklistedAsync(string jti);
}

public class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly InMemoryTokenBlacklistService _fallback = new();
    private readonly ILogger<RedisTokenBlacklistService> _logger;

    public RedisTokenBlacklistService(IConnectionMultiplexer redis, ILogger<RedisTokenBlacklistService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    // Salva o jti com TTL igual ao tempo restante do token — o Redis remove sozinho depois
    public async Task InvalidateTokenAsync(string jti, TimeSpan expiresIn)
    {
        await _fallback.InvalidateTokenAsync(jti, expiresIn);
        try
        {
            await _redis.GetDatabase().StringSetAsync($"blacklist:{jti}", "1", expiresIn);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar blacklist no Redis; usando memória local");
        }
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        if (await _fallback.IsTokenBlacklistedAsync(jti)) return true;
        try
        {
            return await _redis.GetDatabase().KeyExistsAsync($"blacklist:{jti}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar blacklist no Redis");
            return false;
        }
    }
}

/// <summary>Fallback quando o Redis não está configurado/disponível (instância única).</summary>
public class InMemoryTokenBlacklistService : ITokenBlacklistService
{
    private readonly ConcurrentDictionary<string, DateTime> _items = new();

    public Task InvalidateTokenAsync(string jti, TimeSpan expiresIn)
    {
        _items[jti] = DateTime.UtcNow.Add(expiresIn);
        if (_items.Count > 10_000)
        {
            foreach (var kv in _items.Where(kv => kv.Value < DateTime.UtcNow).ToList())
                _items.TryRemove(kv.Key, out _);
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsTokenBlacklistedAsync(string jti)
        => Task.FromResult(_items.TryGetValue(jti, out var exp) && exp > DateTime.UtcNow);
}
