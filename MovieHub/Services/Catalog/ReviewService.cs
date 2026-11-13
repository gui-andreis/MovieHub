using Microsoft.EntityFrameworkCore;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;

namespace MovieHub.Services.Catalog;

public interface IReviewService
{
    Task<PagedResult<ReviewDto>> GetForTitleAsync(int titleId, ReviewQueryParameters p, CancellationToken ct);
    Task<ReviewSummaryDto> GetSummaryAsync(int titleId, CancellationToken ct);
    Task<ReviewDto> CreateAsync(int titleId, ReviewRequest request, CancellationToken ct);
    Task<ReviewDto> UpdateAsync(int id, ReviewRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentProfile _current;

    public ReviewService(ApplicationDbContext db, ICurrentProfile current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<ReviewDto>> GetForTitleAsync(int titleId, ReviewQueryParameters p, CancellationToken ct)
    {
        p.Normalize();
        if (!await _db.Titles.AnyAsync(t => t.Id == titleId, ct))
            throw new NotFoundException($"Título com id {titleId} não encontrado.");

        var profile = await _current.GetProfileAsync(ct);
        var myProfileId = profile?.Id;

        var query = _db.Reviews.AsNoTracking().Where(r => r.TitleId == titleId);
        query = p.Sort?.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(r => r.CreatedAt),
            "rating_desc" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "rating_asc" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .Select(r => new ReviewDto(r.Id, r.TitleId, r.Rating, r.Comment, r.ContainsSpoilers, r.CreatedAt, r.UpdatedAt,
                new ReviewAuthorDto(r.Profile.Name, r.Profile.AvatarKey),
                myProfileId != null && r.ProfileId == myProfileId))
            .ToListAsync(ct);

        return new PagedResult<ReviewDto>(items, p.Page, p.PageSize, total);
    }

    public async Task<ReviewSummaryDto> GetSummaryAsync(int titleId, CancellationToken ct)
    {
        if (!await _db.Titles.AnyAsync(t => t.Id == titleId, ct))
            throw new NotFoundException($"Título com id {titleId} não encontrado.");

        var counts = await _db.Reviews.AsNoTracking()
            .Where(r => r.TitleId == titleId)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var distribution = Enumerable.Range(1, 10).ToDictionary(
            i => i.ToString(),
            i => counts.FirstOrDefault(c => c.Rating == i)?.Count ?? 0);

        var count = counts.Sum(c => c.Count);
        var average = count == 0 ? 0 : Math.Round(counts.Sum(c => c.Rating * c.Count) / (double)count, 1);
        return new ReviewSummaryDto(average, count, distribution);
    }

    public async Task<ReviewDto> CreateAsync(int titleId, ReviewRequest request, CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        if (!await _db.Titles.AnyAsync(t => t.Id == titleId, ct))
            throw new NotFoundException($"Título com id {titleId} não encontrado.");

        if (await _db.Reviews.AnyAsync(r => r.ProfileId == profile.Id && r.TitleId == titleId, ct))
            throw new ConflictException("Este perfil já avaliou este título. Edite a avaliação existente.");

        var review = new Review
        {
            ProfileId = profile.Id,
            TitleId = titleId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ContainsSpoilers = request.ContainsSpoilers
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);

        return ToDto(review, profile, true);
    }

    public async Task<ReviewDto> UpdateAsync(int id, ReviewRequest request, CancellationToken ct)
    {
        var userId = _current.RequireUserId();
        var review = await _db.Reviews.Include(r => r.Profile).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Review com id {id} não encontrada.");

        if (review.Profile.UserId != userId)
            throw new ForbiddenException("Você só pode editar suas próprias avaliações.");

        review.Rating = request.Rating;
        review.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        review.ContainsSpoilers = request.ContainsSpoilers;
        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToDto(review, review.Profile, true);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var userId = _current.RequireUserId();
        var review = await _db.Reviews.Include(r => r.Profile).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"Review com id {id} não encontrada.");

        if (review.Profile.UserId != userId && !_current.IsAdmin)
            throw new ForbiddenException("Você só pode remover suas próprias avaliações.");

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
    }

    private static ReviewDto ToDto(Review r, Profile author, bool isMine) =>
        new(r.Id, r.TitleId, r.Rating, r.Comment, r.ContainsSpoilers, r.CreatedAt, r.UpdatedAt,
            new ReviewAuthorDto(author.Name, author.AvatarKey), isMine);
}
