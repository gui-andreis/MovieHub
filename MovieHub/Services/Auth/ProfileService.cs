using Microsoft.EntityFrameworkCore;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;

namespace MovieHub.Services.Auth;

public interface IProfileService
{
    Task<List<ProfileDto>> GetAllAsync(CancellationToken ct);
    Task<ProfileDto> CreateAsync(ProfileRequest request, CancellationToken ct);
    Task<ProfileDto> UpdateAsync(Guid id, ProfileRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class ProfileService : IProfileService
{
    public const int MaxProfiles = 5;

    public static readonly IReadOnlyList<string> Avatars =
        Enumerable.Range(1, 12).Select(i => $"avatar-{i:00}").ToList();

    private readonly ApplicationDbContext _db;
    private readonly ICurrentProfile _current;

    public ProfileService(ApplicationDbContext db, ICurrentProfile current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<ProfileDto>> GetAllAsync(CancellationToken ct)
    {
        var userId = _current.RequireUserId();
        return await _db.Profiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new ProfileDto(p.Id, p.Name, p.AvatarKey, p.IsKids, p.MaturityLimit, p.Language, p.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProfileDto> CreateAsync(ProfileRequest request, CancellationToken ct)
    {
        var userId = _current.RequireUserId();
        var count = await _db.Profiles.CountAsync(p => p.UserId == userId, ct);
        if (count >= MaxProfiles)
            throw new BadRequestException($"Limite de {MaxProfiles} perfis por conta atingido.");

        var profile = new Profile { UserId = userId };
        Apply(profile, request, count);
        _db.Profiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return ToDto(profile);
    }

    public async Task<ProfileDto> UpdateAsync(Guid id, ProfileRequest request, CancellationToken ct)
    {
        var profile = await GetOwnedAsync(id, ct);
        Apply(profile, request, null);
        await _db.SaveChangesAsync(ct);
        return ToDto(profile);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var profile = await GetOwnedAsync(id, ct);
        var count = await _db.Profiles.CountAsync(p => p.UserId == profile.UserId, ct);
        if (count <= 1)
            throw new BadRequestException("A conta precisa ter pelo menos um perfil.");

        _db.Profiles.Remove(profile);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Profile> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var userId = _current.RequireUserId();
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Perfil não encontrado.");
        if (profile.UserId != userId)
            throw new ForbiddenException("Este perfil não pertence a você.");
        return profile;
    }

    private static void Apply(Profile profile, ProfileRequest request, int? existingCount)
    {
        if (request.MaturityLimit != null && !AgeRatings.IsValid(request.MaturityLimit))
            throw new BadRequestException("maturityLimit deve ser L, 10, 12, 14, 16 ou 18.");

        if (request.AvatarKey != null && !Avatars.Contains(request.AvatarKey))
            throw new BadRequestException("avatarKey inválido. Consulte GET /api/profiles/avatars.");

        profile.Name = request.Name.Trim();
        profile.IsKids = request.IsKids;
        profile.AvatarKey = request.AvatarKey
            ?? (existingCount.HasValue ? Avatars[existingCount.Value % Avatars.Count] : profile.AvatarKey);
        profile.MaturityLimit = request.MaturityLimit ?? (request.IsKids ? "10" : (existingCount.HasValue ? "18" : profile.MaturityLimit));
        if (profile.IsKids && (AgeRatings.Rank(profile.MaturityLimit) ?? 18) > 10)
            profile.MaturityLimit = "10";
        profile.Language = string.IsNullOrWhiteSpace(request.Language) ? profile.Language : request.Language;
    }

    private static ProfileDto ToDto(Profile p) => new(p.Id, p.Name, p.AvatarKey, p.IsKids, p.MaturityLimit, p.Language, p.CreatedAt);
}
