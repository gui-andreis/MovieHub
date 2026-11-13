using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Exceptions;
using MovieHub.Models;

namespace MovieHub.Services;

/// <summary>
/// Contexto do usuário/perfil da requisição atual. O perfil vem do header <c>X-Profile-Id</c>
/// e é validado contra o usuário do token (senão 403).
/// </summary>
public interface ICurrentProfile
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    /// <summary>Perfil da requisição, ou null se não houver header/autenticação.</summary>
    Task<Profile?> GetProfileAsync(CancellationToken ct = default);

    /// <summary>Perfil obrigatório (400 se o header estiver ausente).</summary>
    Task<Profile> RequireProfileAsync(CancellationToken ct = default);

    /// <summary>Limite de classificação aplicável (null = sem restrição).</summary>
    Task<int?> GetMaturityLimitAsync(CancellationToken ct = default);

    string RequireUserId();
}

public sealed class CurrentProfile : ICurrentProfile
{
    public const string HeaderName = "X-Profile-Id";

    private readonly IHttpContextAccessor _http;
    private readonly ApplicationDbContext _db;
    private Profile? _profile;
    private bool _resolved;

    public CurrentProfile(IHttpContextAccessor http, ApplicationDbContext db)
    {
        _http = http;
        _db = db;
    }

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    public string? UserId => User?.Identity?.IsAuthenticated == true
        ? User.FindFirstValue(ClaimTypes.NameIdentifier)
        : null;

    public bool IsAuthenticated => UserId != null;
    public bool IsAdmin => User?.IsInRole("Admin") == true;

    public string RequireUserId() => UserId ?? throw new UnauthorizedException("Autenticação necessária.");

    public async Task<Profile?> GetProfileAsync(CancellationToken ct = default)
    {
        if (_resolved) return _profile;
        _resolved = true;

        var userId = UserId;
        var header = _http.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
        if (userId == null || string.IsNullOrWhiteSpace(header)) return null;

        if (!Guid.TryParse(header, out var profileId))
            throw new BadRequestException($"Header {HeaderName} inválido.");

        _profile = await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId, ct);
        if (_profile == null) throw new NotFoundException("Perfil não encontrado.");
        if (_profile.UserId != userId) throw new ForbiddenException("Este perfil não pertence ao usuário autenticado.");
        return _profile;
    }

    public async Task<Profile> RequireProfileAsync(CancellationToken ct = default)
    {
        RequireUserId();
        return await GetProfileAsync(ct)
            ?? throw new BadRequestException($"Selecione um perfil (header {HeaderName}).");
    }

    public async Task<int?> GetMaturityLimitAsync(CancellationToken ct = default)
    {
        var profile = await GetProfileAsync(ct);
        return profile == null ? null : AgeRatings.EffectiveLimit(profile.MaturityLimit, profile.IsKids);
    }
}
