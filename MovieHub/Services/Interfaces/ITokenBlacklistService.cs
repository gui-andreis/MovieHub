namespace MovieHub.Services.Interfaces;

public interface ITokenBlacklistService
{
    Task InvalidateTokenAsync(string jti, TimeSpan expiresIn);
    Task<bool> IsTokenBlacklistedAsync(string jti);
}