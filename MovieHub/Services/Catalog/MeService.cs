using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;

namespace MovieHub.Services.Catalog;

public interface IMeService
{
    Task<List<TitleCardDto>> GetMyListAsync(CancellationToken ct);
    Task<MyListStatusDto> AddToListAsync(int titleId, CancellationToken ct);
    Task<MyListStatusDto> RemoveFromListAsync(int titleId, CancellationToken ct);
    Task<List<ContinueWatchingDto>> GetContinueWatchingAsync(CancellationToken ct);
    Task<ProgressResultDto> SaveProgressAsync(ProgressRequest request, CancellationToken ct);
    Task RemoveProgressAsync(int titleId, CancellationToken ct);
    Task<ReactionDto> SetReactionAsync(int titleId, ReactionValue value, CancellationToken ct);
    Task<ReactionDto> RemoveReactionAsync(int titleId, CancellationToken ct);
    Task<List<MyReviewDto>> GetMyReviewsAsync(CancellationToken ct);
    Task<MeStatsDto> GetStatsAsync(CancellationToken ct);
}

public class MeService : IMeService
{
    /// <summary>A partir de 90% o item é considerado assistido.</summary>
    public const double CompletionThreshold = 0.9;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentProfile _current;

    public MeService(ApplicationDbContext db, ICurrentProfile current)
    {
        _db = db;
        _current = current;
    }

    private async Task EnsureTitleExistsAsync(int titleId, CancellationToken ct)
    {
        if (!await _db.Titles.AnyAsync(t => t.Id == titleId, ct))
            throw new NotFoundException($"Título com id {titleId} não encontrado.");
    }

    // ---------- Minha lista ----------

    public async Task<List<TitleCardDto>> GetMyListAsync(CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        var limit = await _current.GetMaturityLimitAsync(ct);
        return await _db.MyListItems.AsNoTracking()
            .Where(m => m.ProfileId == profile.Id)
            .OrderByDescending(m => m.AddedAt)
            .Select(m => m.Title)
            .ForMaturity(limit)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);
    }

    public async Task<MyListStatusDto> AddToListAsync(int titleId, CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        await EnsureTitleExistsAsync(titleId, ct);

        if (!await _db.MyListItems.AnyAsync(m => m.ProfileId == profile.Id && m.TitleId == titleId, ct))
        {
            _db.MyListItems.Add(new MyListItem { ProfileId = profile.Id, TitleId = titleId });
            await _db.SaveChangesAsync(ct);
        }
        return new MyListStatusDto(titleId, true);
    }

    public async Task<MyListStatusDto> RemoveFromListAsync(int titleId, CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        var item = await _db.MyListItems.FirstOrDefaultAsync(m => m.ProfileId == profile.Id && m.TitleId == titleId, ct);
        if (item != null)
        {
            _db.MyListItems.Remove(item);
            await _db.SaveChangesAsync(ct);
        }
        return new MyListStatusDto(titleId, false);
    }

    // ---------- Progresso / continuar assistindo ----------

    public async Task<List<ContinueWatchingDto>> GetContinueWatchingAsync(CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        var limit = await _current.GetMaturityLimitAsync(ct);

        var progress = await _db.WatchProgress.AsNoTracking()
            .Where(w => w.ProfileId == profile.Id)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new
            {
                w.TitleId,
                w.EpisodeId,
                w.PositionSeconds,
                w.DurationSeconds,
                w.Completed,
                w.UpdatedAt,
                Episode = w.Episode == null ? null : new EpisodeRefDto(w.Episode.Id, w.Episode.Season.SeasonNumber, w.Episode.EpisodeNumber, w.Episode.Name)
            })
            .Take(300)
            .ToListAsync(ct);

        // Último registro por título; só entra se ainda não foi concluído
        var latest = progress
            .GroupBy(p => p.TitleId)
            .Select(g => g.First())
            .Where(p => !p.Completed)
            .Take(20)
            .ToList();

        var ids = latest.Select(p => p.TitleId).ToList();
        var cards = await _db.Titles.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ForMaturity(limit)
            .Select(TitleQueries.ToCard)
            .ToDictionaryAsync(t => t.Id, ct);

        return latest
            .Where(p => cards.ContainsKey(p.TitleId))
            .Select(p => new ContinueWatchingDto(cards[p.TitleId], p.Episode, p.PositionSeconds, p.DurationSeconds,
                TitleQueries.Percent(p.PositionSeconds, p.DurationSeconds), p.UpdatedAt))
            .ToList();
    }

    public async Task<ProgressResultDto> SaveProgressAsync(ProgressRequest request, CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        var title = await _db.Titles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TitleId, ct)
            ?? throw new NotFoundException($"Título com id {request.TitleId} não encontrado.");

        Episode? episode = null;
        if (request.EpisodeId.HasValue)
        {
            episode = await _db.Episodes.AsNoTracking().Include(e => e.Season)
                .FirstOrDefaultAsync(e => e.Id == request.EpisodeId, ct);
            if (episode == null || episode.Season.TitleId != title.Id)
                throw new BadRequestException("Episódio não pertence a este título.");
        }
        else if (title.Type == TitleType.Series)
        {
            // Série sem episódio informado: usa o primeiro episódio disponível
            episode = await _db.Episodes.AsNoTracking().Include(e => e.Season)
                .Where(e => e.Season.TitleId == title.Id && e.Season.SeasonNumber > 0)
                .OrderBy(e => e.Season.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                .FirstOrDefaultAsync(ct);
        }

        var position = Math.Min(request.PositionSeconds, request.DurationSeconds);
        var completed = position >= request.DurationSeconds * CompletionThreshold;
        var now = DateTime.UtcNow;

        var entry = await UpsertProgressAsync(profile.Id, title.Id, episode?.Id, ct);
        entry.PositionSeconds = position;
        entry.DurationSeconds = request.DurationSeconds;
        entry.Completed = completed;
        entry.UpdatedAt = now;

        EpisodeRefDto? next = null;
        if (completed && episode != null)
        {
            var nextEpisode = await _db.Episodes.AsNoTracking().Include(e => e.Season)
                .Where(e => e.Season.TitleId == title.Id && e.Season.SeasonNumber > 0)
                .Where(e => (e.Season.SeasonNumber == episode.Season.SeasonNumber && e.EpisodeNumber > episode.EpisodeNumber)
                            || e.Season.SeasonNumber > episode.Season.SeasonNumber)
                .OrderBy(e => e.Season.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                .FirstOrDefaultAsync(ct);

            if (nextEpisode != null)
            {
                next = new EpisodeRefDto(nextEpisode.Id, nextEpisode.Season.SeasonNumber, nextEpisode.EpisodeNumber, nextEpisode.Name);

                // Coloca o próximo episódio em "continuar assistindo" (posição zero), um tick depois
                var nextEntry = await UpsertProgressAsync(profile.Id, title.Id, nextEpisode.Id, ct);
                if (!nextEntry.Completed)
                {
                    nextEntry.DurationSeconds = (nextEpisode.RuntimeMinutes ?? title.RuntimeMinutes ?? 45) * 60;
                    nextEntry.UpdatedAt = now.AddMilliseconds(1);
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ProgressResultDto(title.Id, episode?.Id, position, request.DurationSeconds,
            TitleQueries.Percent(position, request.DurationSeconds), completed, next);
    }

    private async Task<WatchProgress> UpsertProgressAsync(Guid profileId, int titleId, int? episodeId, CancellationToken ct)
    {
        var local = _db.WatchProgress.Local.FirstOrDefault(w => w.ProfileId == profileId && w.TitleId == titleId && w.EpisodeId == episodeId);
        if (local != null) return local;

        var entry = await _db.WatchProgress
            .FirstOrDefaultAsync(w => w.ProfileId == profileId && w.TitleId == titleId && w.EpisodeId == episodeId, ct);
        if (entry != null) return entry;

        entry = new WatchProgress { ProfileId = profileId, TitleId = titleId, EpisodeId = episodeId };
        _db.WatchProgress.Add(entry);
        return entry;
    }

    public async Task RemoveProgressAsync(int titleId, CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        var entries = await _db.WatchProgress.Where(w => w.ProfileId == profile.Id && w.TitleId == titleId).ToListAsync(ct);
        if (entries.Count == 0) return;

        // Mantém episódios já concluídos (histórico/estatísticas) e remove o que estava em andamento
        _db.WatchProgress.RemoveRange(entries.Where(e => !e.Completed));
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Reações ----------

    public async Task<ReactionDto> SetReactionAsync(int titleId, ReactionValue value, CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        await EnsureTitleExistsAsync(titleId, ct);

        var reaction = await _db.Reactions.FirstOrDefaultAsync(r => r.ProfileId == profile.Id && r.TitleId == titleId, ct);
        if (reaction == null)
        {
            reaction = new Reaction { ProfileId = profile.Id, TitleId = titleId };
            _db.Reactions.Add(reaction);
        }
        reaction.Value = value;
        reaction.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new ReactionDto(titleId, value);
    }

    public async Task<ReactionDto> RemoveReactionAsync(int titleId, CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        var reaction = await _db.Reactions.FirstOrDefaultAsync(r => r.ProfileId == profile.Id && r.TitleId == titleId, ct);
        if (reaction != null)
        {
            _db.Reactions.Remove(reaction);
            await _db.SaveChangesAsync(ct);
        }
        return new ReactionDto(titleId, null);
    }

    // ---------- Reviews / estatísticas ----------

    public async Task<List<MyReviewDto>> GetMyReviewsAsync(CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);
        var reviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProfileId == profile.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.Rating, r.Comment, r.ContainsSpoilers, r.CreatedAt, r.UpdatedAt, r.TitleId })
            .ToListAsync(ct);

        var ids = reviews.Select(r => r.TitleId).ToList();
        var cards = await _db.Titles.AsNoTracking().Where(t => ids.Contains(t.Id))
            .Select(TitleQueries.ToCard).ToDictionaryAsync(t => t.Id, ct);

        return reviews
            .Where(r => cards.ContainsKey(r.TitleId))
            .Select(r => new MyReviewDto(r.Id, r.Rating, r.Comment, r.ContainsSpoilers, r.CreatedAt, r.UpdatedAt, cards[r.TitleId]))
            .ToList();
    }

    public async Task<MeStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var profile = await _current.RequireProfileAsync(ct);

        var progress = await _db.WatchProgress.AsNoTracking()
            .Where(w => w.ProfileId == profile.Id)
            .Select(w => new { w.TitleId, w.PositionSeconds, w.DurationSeconds, w.Completed })
            .ToListAsync(ct);

        var watchedTitleIds = progress.Where(p => p.Completed).Select(p => p.TitleId).Distinct().ToList();
        var touchedTitleIds = progress.Where(p => p.PositionSeconds > 0).Select(p => p.TitleId).Distinct().ToList();
        var seconds = progress.Sum(p => p.Completed ? Math.Max(p.PositionSeconds, p.DurationSeconds) : p.PositionSeconds);

        var likedIds = await _db.Reactions.AsNoTracking()
            .Where(r => r.ProfileId == profile.Id && r.Value != ReactionValue.Dislike)
            .Select(r => r.TitleId).ToListAsync(ct);

        var genreSourceIds = touchedTitleIds.Union(likedIds).ToList();
        var genreNames = await _db.TitleGenres.AsNoTracking()
            .Where(tg => genreSourceIds.Contains(tg.TitleId))
            .Select(tg => tg.Genre.Name)
            .ToListAsync(ct);
        var topGenres = genreNames
            .GroupBy(n => n)
            .Select(g => new GenreCountDto(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Name)
            .Take(5)
            .ToList();

        return new MeStatsDto(
            WatchedCount: watchedTitleIds.Count,
            MinutesWatched: seconds / 60,
            TopGenres: topGenres,
            ReviewsCount: await _db.Reviews.CountAsync(r => r.ProfileId == profile.Id, ct),
            ListCount: await _db.MyListItems.CountAsync(m => m.ProfileId == profile.Id, ct),
            LikesCount: likedIds.Count);
    }
}
