using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Models;

namespace MovieHub.Services.Catalog;

public interface IBrowseService
{
    Task<HomeDto> GetHomeAsync(string? type, CancellationToken ct);
}

/// <summary>Monta a home estilo streaming: hero + linhas temáticas (personalizadas quando há perfil).</summary>
public class BrowseService : IBrowseService
{
    private const int RowSize = 20;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentProfile _current;
    private readonly ICatalogService _catalog;
    private readonly IMeService _me;

    public BrowseService(ApplicationDbContext db, ICurrentProfile current, ICatalogService catalog, IMeService me)
    {
        _db = db;
        _current = current;
        _catalog = catalog;
        _me = me;
    }

    public async Task<HomeDto> GetHomeAsync(string? type, CancellationToken ct)
    {
        var titleType = TitleQueries.ParseType(type);
        var limit = await _current.GetMaturityLimitAsync(ct);
        var profile = await _current.GetProfileAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        IQueryable<Title> Base() => _db.Titles.AsNoTracking().ForMaturity(limit).OfType(titleType).Where(t => t.PosterUrl != null);
        Task<List<TitleCardDto>> Cards(IQueryable<Title> q, int take = RowSize) => q.Take(take).Select(TitleQueries.ToCard).ToListAsync(ct);

        var rows = new List<HomeRowDto>();
        void Add(string key, string title, string kind, List<TitleCardDto> items)
        {
            if (items.Count > 0) rows.Add(new HomeRowDto(key, title, kind, items));
        }

        // Hero: destaque marcado pelo admin, senão um dos mais populares com backdrop + trailer
        var heroQuery = Base().Where(t => t.BackdropUrl != null);
        var featured = heroQuery.Where(t => t.IsFeatured);
        var heroPool = await featured.AnyAsync(ct)
            ? featured
            : heroQuery.Where(t => t.TrailerKey != null && t.Overview != "").OrderByDescending(t => t.Popularity).Take(8);
        var heroIds = await heroPool.Select(t => t.Id).ToListAsync(ct);
        HeroDto? hero = null;
        if (heroIds.Count > 0)
        {
            var heroId = heroIds[Random.Shared.Next(heroIds.Count)];
            hero = await _db.Titles.AsNoTracking().Where(t => t.Id == heroId).Select(TitleQueries.ToHero).FirstAsync(ct);
        }

        if (profile != null)
        {
            var continueWatching = (await _me.GetContinueWatchingAsync(ct))
                .Where(c => titleType == null || c.Title.Type == titleType)
                .Select(c => c.Title).ToList();
            Add("continue-watching", $"Continuar assistindo como {profile.Name}", "continue", continueWatching);

            var myList = await _db.MyListItems.AsNoTracking()
                .Where(m => m.ProfileId == profile.Id)
                .OrderByDescending(m => m.AddedAt)
                .Select(m => m.Title)
                .ForMaturity(limit).OfType(titleType)
                .Take(RowSize).Select(TitleQueries.ToCard).ToListAsync(ct);
            Add("my-list", "Minha lista", "mylist", myList);
        }

        var top10Title = titleType switch
        {
            TitleType.Movie => "Top 10 filmes hoje",
            TitleType.Series => "Top 10 séries hoje",
            _ => "Top 10 no MovieHub hoje"
        };
        Add("top10", top10Title, "top10",
            await Cards(Base().Where(t => t.ReleaseDate == null || t.ReleaseDate <= today).OrderByDescending(t => t.Popularity), 10));

        Add("trending", "Em alta", "standard",
            await Cards(Base().OrderByDescending(t => t.Popularity).Skip(10)));

        if (profile != null)
        {
            var seed = await _db.Reactions.AsNoTracking()
                .Where(r => r.ProfileId == profile.Id && r.Value != ReactionValue.Dislike)
                .OrderByDescending(r => r.Value).ThenByDescending(r => r.UpdatedAt)
                .Select(r => new { r.TitleId, r.Title.Name })
                .FirstOrDefaultAsync(ct)
                ?? await _db.MyListItems.AsNoTracking()
                    .Where(m => m.ProfileId == profile.Id)
                    .OrderByDescending(m => m.AddedAt)
                    .Select(m => new { m.TitleId, m.Title.Name })
                    .FirstOrDefaultAsync(ct);

            if (seed != null)
            {
                var similar = await _catalog.GetSimilarAsync(seed.TitleId, RowSize, ct);
                Add($"because-{seed.TitleId}", $"Porque você curtiu {seed.Name}", "standard",
                    similar.Where(s => titleType == null || s.Type == titleType).ToList());
            }
        }

        Add("new-releases", "Lançamentos", "standard",
            await Cards(Base().Where(t => t.ReleaseDate != null && t.ReleaseDate <= today).OrderByDescending(t => t.ReleaseDate)));

        Add("acclaimed", "Aclamados pela crítica", "standard",
            await Cards(Base().Where(t => t.VoteCount >= 500).OrderByDescending(t => t.VoteAverage)));

        // Linhas por gênero: os gêneros com mais títulos
        var topGenres = await _db.Genres.AsNoTracking()
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Slug,
                Count = g.TitleGenres.Count(tg => (titleType == null || tg.Title.Type == titleType)
                    && (limit == null || (tg.Title.AgeRatingRank != null && tg.Title.AgeRatingRank <= limit)))
            })
            .Where(g => g.Count >= 6)
            .OrderByDescending(g => g.Count)
            .Take(6)
            .ToListAsync(ct);

        foreach (var genre in topGenres)
        {
            var items = await Cards(Base()
                .Where(t => t.TitleGenres.Any(tg => tg.GenreId == genre.Id))
                .OrderByDescending(t => t.Popularity));
            Add($"genre-{genre.Slug}", genre.Name, "standard", items);
        }

        if (titleType != TitleType.Series)
        {
            var sagaTitles = await Base()
                .Where(t => t.CollectionId != null)
                .Select(t => new { t.Id, t.CollectionId, t.ReleaseDate })
                .ToListAsync(ct);
            var sagaIds = sagaTitles
                .GroupBy(t => t.CollectionId)
                .Where(g => g.Count() >= 2)
                .Select(g => g.OrderBy(t => t.ReleaseDate).First().Id)
                .ToList();
            Add("sagas", "Sagas para maratonar", "standard",
                await Cards(Base().Where(t => sagaIds.Contains(t.Id)).OrderByDescending(t => t.Popularity)));
        }

        Add("hidden-gems", "Escondidos que valem a pena", "standard",
            await Cards(Base().Where(t => t.VoteAverage >= 7.2 && t.VoteCount >= 150)
                .OrderBy(t => t.Popularity).ThenByDescending(t => t.VoteAverage)));

        return new HomeDto(hero, rows);
    }
}
