using Microsoft.EntityFrameworkCore;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Tmdb;

namespace MovieHub.Services.Catalog;

public interface ICatalogService
{
    Task<PagedResult<TitleCardDto>> ListAsync(TitleQueryParameters p, CancellationToken ct);
    Task<TitleDetailDto> GetDetailAsync(int id, CancellationToken ct);
    Task<List<TitleCardDto>> GetSimilarAsync(int id, int limit, CancellationToken ct);
    Task<SeasonDetailDto> GetSeasonAsync(int titleId, int seasonNumber, CancellationToken ct);
    Task<List<TitleCardDto>> GetTrendingAsync(string? type, int limit, CancellationToken ct);
    Task<List<TitleCardDto>> GetTop10Async(string? type, CancellationToken ct);
    Task<TitleCardDto> GetRandomAsync(string? type, int? genreId, CancellationToken ct);
    Task<List<TitleCardDto>> GetUpcomingAsync(int limit, CancellationToken ct);
    Task<SearchResultDto> SearchAsync(string? q, CancellationToken ct);
    Task<List<GenreDto>> GetGenresAsync(CancellationToken ct);
    Task<PagedResult<TitleCardDto>> GetGenreTitlesAsync(string slug, TitleQueryParameters p, CancellationToken ct);
    Task<PersonDetailDto> GetPersonAsync(int id, CancellationToken ct);
    Task<CollectionDetailDto> GetCollectionAsync(int id, CancellationToken ct);
    Task<List<ProviderDto>> GetProvidersAsync(CancellationToken ct);
}

public class CatalogService : ICatalogService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentProfile _current;
    private readonly ITmdbImporter? _importer;

    public CatalogService(ApplicationDbContext db, ICurrentProfile current, ITmdbImporter? importer = null)
    {
        _db = db;
        _current = current;
        _importer = importer;
    }

    private async Task<IQueryable<Title>> VisibleTitlesAsync(CancellationToken ct)
    {
        var limit = await _current.GetMaturityLimitAsync(ct);
        return _db.Titles.AsNoTracking().ForMaturity(limit);
    }

    public async Task<PagedResult<TitleCardDto>> ListAsync(TitleQueryParameters p, CancellationToken ct)
    {
        p.Normalize();
        var query = await VisibleTitlesAsync(ct);

        query = query.Search(p.Q).OfType(TitleQueries.ParseType(p.Type));

        var genreIds = TitleQueries.ParseCsv(p.GenreIds);
        if (genreIds.Count > 0)
            query = query.Where(t => t.TitleGenres.Any(g => genreIds.Contains(g.GenreId)));

        var providerIds = TitleQueries.ParseCsv(p.ProviderIds);
        if (providerIds.Count > 0)
            query = query.Where(t => t.TitleProviders.Any(tp => providerIds.Contains(tp.ProviderId)));

        if (p.YearFrom.HasValue) query = query.Where(t => t.Year >= p.YearFrom);
        if (p.YearTo.HasValue) query = query.Where(t => t.Year <= p.YearTo);
        if (p.MinRating.HasValue) query = query.Where(t => t.VoteAverage >= p.MinRating);
        if (!string.IsNullOrWhiteSpace(p.Language))
        {
            var lang = p.Language.Trim().ToLowerInvariant();
            query = query.Where(t => t.OriginalLanguage == lang);
        }

        var maxRank = AgeRatings.Rank(p.MaxAgeRating);
        if (maxRank.HasValue) query = query.Where(t => t.AgeRatingRank != null && t.AgeRatingRank <= maxRank);

        var total = await query.CountAsync(ct);
        var items = await query.ApplySort(p.Sort)
            .Skip((p.Page - 1) * p.PageSize)
            .Take(p.PageSize)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);

        return new PagedResult<TitleCardDto>(items, p.Page, p.PageSize, total);
    }

    public async Task<TitleDetailDto> GetDetailAsync(int id, CancellationToken ct)
    {
        var title = await _db.Titles.AsNoTracking()
            .Include(t => t.Collection)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Título com id {id} não encontrado.");

        await EnsureAllowedAsync(title, ct);

        var dto = new TitleDetailDto
        {
            Id = title.Id,
            TmdbId = title.TmdbId,
            Type = title.Type,
            Name = title.Name,
            OriginalName = title.OriginalName,
            Tagline = title.Tagline,
            Overview = title.Overview,
            ReleaseDate = title.ReleaseDate,
            Year = title.Year,
            RuntimeMinutes = title.RuntimeMinutes,
            PosterUrl = title.PosterUrl,
            BackdropUrl = title.BackdropUrl,
            LogoUrl = title.LogoUrl,
            TrailerKey = title.TrailerKey,
            AgeRating = title.AgeRating,
            VoteAverage = title.VoteAverage,
            VoteCount = title.VoteCount,
            Popularity = title.Popularity,
            OriginalLanguage = title.OriginalLanguage,
            Status = title.Status,
            NumberOfSeasons = title.NumberOfSeasons,
            NumberOfEpisodes = title.NumberOfEpisodes,
            IsFeatured = title.IsFeatured,
            Collection = title.Collection == null ? null
                : new CollectionRefDto(title.Collection.Id, title.Collection.Name, title.Collection.PosterUrl)
        };

        dto.Genres = await _db.TitleGenres.AsNoTracking()
            .Where(g => g.TitleId == id)
            .OrderBy(g => g.Genre.Name)
            .Select(g => new GenreRefDto(g.Genre.Id, g.Genre.Name, g.Genre.Slug))
            .ToListAsync(ct);

        dto.Cast = await _db.Credits.AsNoTracking()
            .Where(c => c.TitleId == id && c.Role == CreditRole.Cast)
            .OrderBy(c => c.Order).Take(20)
            .Select(c => new CastDto(c.PersonId, c.Person.Name, c.Character, c.Person.ProfileUrl))
            .ToListAsync(ct);

        dto.Crew = await _db.Credits.AsNoTracking()
            .Where(c => c.TitleId == id && c.Role == CreditRole.Crew)
            .OrderBy(c => c.Order)
            .Select(c => new CrewDto(c.PersonId, c.Person.Name, c.Job, c.Person.ProfileUrl))
            .ToListAsync(ct);

        dto.Providers = await _db.TitleProviders.AsNoTracking()
            .Where(tp => tp.TitleId == id)
            .OrderBy(tp => tp.Kind).ThenBy(tp => tp.Provider.DisplayPriority)
            .Select(tp => new ProviderRefDto(tp.Provider.Id, tp.Provider.Name, tp.Provider.LogoUrl, tp.Kind))
            .ToListAsync(ct);

        dto.Seasons = await _db.Seasons.AsNoTracking()
            .Where(s => s.TitleId == id)
            .OrderBy(s => s.SeasonNumber)
            .Select(s => new SeasonSummaryDto(s.Id, s.SeasonNumber, s.Name, s.EpisodeCount, s.PosterUrl, s.AirDate))
            .ToListAsync(ct);

        var reviews = _db.Reviews.AsNoTracking().Where(r => r.TitleId == id);
        var reviewCount = await reviews.CountAsync(ct);
        var reviewAvg = reviewCount == 0 ? 0 : await reviews.AverageAsync(r => (double)r.Rating, ct);
        dto.Rating = new RatingSummaryDto(Math.Round(reviewAvg, 1), reviewCount);

        var profile = await _current.GetProfileAsync(ct);
        if (profile != null)
            dto.Me = await BuildMeAsync(profile.Id, id, ct);

        return dto;
    }

    private async Task<TitleMeDto> BuildMeAsync(Guid profileId, int titleId, CancellationToken ct)
    {
        var me = new TitleMeDto
        {
            InMyList = await _db.MyListItems.AnyAsync(m => m.ProfileId == profileId && m.TitleId == titleId, ct),
            Reaction = await _db.Reactions.Where(r => r.ProfileId == profileId && r.TitleId == titleId)
                .Select(r => (ReactionValue?)r.Value).FirstOrDefaultAsync(ct)
        };

        var progress = await _db.WatchProgress.AsNoTracking()
            .Where(w => w.ProfileId == profileId && w.TitleId == titleId)
            .OrderByDescending(w => w.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (progress != null)
            me.Progress = new ProgressDto(progress.EpisodeId, progress.PositionSeconds, progress.DurationSeconds,
                TitleQueries.Percent(progress.PositionSeconds, progress.DurationSeconds), progress.Completed);

        me.Review = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProfileId == profileId && r.TitleId == titleId)
            .Select(r => new MyReviewRefDto(r.Id, r.Rating, r.Comment, r.ContainsSpoilers, r.CreatedAt, r.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        return me;
    }

    private async Task EnsureAllowedAsync(Title title, CancellationToken ct)
    {
        var limit = await _current.GetMaturityLimitAsync(ct);
        if (limit != null && (title.AgeRatingRank == null || title.AgeRatingRank > limit))
            throw new ForbiddenException("Este título não está disponível para este perfil.");
    }

    public async Task<List<TitleCardDto>> GetSimilarAsync(int id, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 40);
        var source = await _db.Titles.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Type,
                t.CollectionId,
                Genres = t.TitleGenres.Select(g => g.GenreId).ToList(),
                Keywords = t.TitleKeywords.Select(k => k.KeywordId).ToList()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Título com id {id} não encontrado.");

        var genres = source.Genres;
        var keywords = source.Keywords;
        var collectionId = source.CollectionId;
        var type = source.Type;

        var query = (await VisibleTitlesAsync(ct))
            .Where(t => t.Id != id)
            .Where(t => (collectionId != null && t.CollectionId == collectionId)
                        || t.TitleGenres.Any(g => genres.Contains(g.GenreId))
                        || t.TitleKeywords.Any(k => keywords.Contains(k.KeywordId)));

        return await query
            .Select(t => new
            {
                Title = t,
                Score = t.TitleGenres.Count(g => genres.Contains(g.GenreId)) * 2
                        + t.TitleKeywords.Count(k => keywords.Contains(k.KeywordId)) * 3
                        + (collectionId != null && t.CollectionId == collectionId ? 15 : 0)
                        + (t.Type == type ? 2 : 0)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Title.VoteAverage)
            .ThenByDescending(x => x.Title.Popularity)
            .Select(x => x.Title)
            .Take(limit)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);
    }

    public async Task<SeasonDetailDto> GetSeasonAsync(int titleId, int seasonNumber, CancellationToken ct)
    {
        var title = await _db.Titles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == titleId, ct)
            ?? throw new NotFoundException($"Título com id {titleId} não encontrado.");
        await EnsureAllowedAsync(title, ct);

        var season = await _db.Seasons.AsNoTracking()
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.TitleId == titleId && s.SeasonNumber == seasonNumber, ct)
            ?? throw new NotFoundException($"Temporada {seasonNumber} não encontrada.");

        var dto = new SeasonDetailDto
        {
            Id = season.Id,
            TitleId = titleId,
            SeasonNumber = season.SeasonNumber,
            Name = season.Name,
            Overview = season.Overview,
            PosterUrl = season.PosterUrl,
            AirDate = season.AirDate,
            EpisodeCount = season.EpisodeCount,
            Episodes = season.Episodes.OrderBy(e => e.EpisodeNumber).Select(e => new EpisodeDto
            {
                Id = e.Id,
                EpisodeNumber = e.EpisodeNumber,
                Name = e.Name,
                Overview = e.Overview,
                StillUrl = e.StillUrl,
                RuntimeMinutes = e.RuntimeMinutes,
                AirDate = e.AirDate
            }).ToList()
        };

        var profile = await _current.GetProfileAsync(ct);
        if (profile != null && dto.Episodes.Count > 0)
        {
            var episodeIds = dto.Episodes.Select(e => e.Id).ToList();
            var progress = await _db.WatchProgress.AsNoTracking()
                .Where(w => w.ProfileId == profile.Id && w.EpisodeId != null && episodeIds.Contains(w.EpisodeId.Value))
                .ToListAsync(ct);
            var byEpisode = progress.GroupBy(p => p.EpisodeId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());
            foreach (var ep in dto.Episodes)
            {
                if (byEpisode.TryGetValue(ep.Id, out var w))
                    ep.Progress = new ProgressDto(w.EpisodeId, w.PositionSeconds, w.DurationSeconds,
                        TitleQueries.Percent(w.PositionSeconds, w.DurationSeconds), w.Completed);
            }
        }

        return dto;
    }

    public async Task<List<TitleCardDto>> GetTrendingAsync(string? type, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 50);
        return await (await VisibleTitlesAsync(ct))
            .OfType(TitleQueries.ParseType(type))
            .Where(t => t.PosterUrl != null)
            .OrderByDescending(t => t.Popularity)
            .Take(limit)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);
    }

    public async Task<List<TitleCardDto>> GetTop10Async(string? type, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await (await VisibleTitlesAsync(ct))
            .OfType(TitleQueries.ParseType(type))
            .Where(t => t.ReleaseDate == null || t.ReleaseDate <= today)
            .OrderByDescending(t => t.Popularity)
            .Take(10)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);
    }

    public async Task<TitleCardDto> GetRandomAsync(string? type, int? genreId, CancellationToken ct)
    {
        var query = (await VisibleTitlesAsync(ct))
            .OfType(TitleQueries.ParseType(type))
            .Where(t => t.VoteAverage >= 6.5 && t.PosterUrl != null);

        if (genreId.HasValue)
            query = query.Where(t => t.TitleGenres.Any(g => g.GenreId == genreId));

        var count = await query.CountAsync(ct);
        if (count == 0) throw new NotFoundException("Nenhum título encontrado para sortear.");

        var skip = Random.Shared.Next(count);
        return await query.OrderBy(t => t.Id).Skip(skip).Take(1).Select(TitleQueries.ToCard).FirstAsync(ct);
    }

    public async Task<List<TitleCardDto>> GetUpcomingAsync(int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 50);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await (await VisibleTitlesAsync(ct))
            .Where(t => t.ReleaseDate > today)
            .OrderBy(t => t.ReleaseDate)
            .Take(limit)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);
    }

    public async Task<SearchResultDto> SearchAsync(string? q, CancellationToken ct)
    {
        var term = TextNormalizer.Normalize(q);
        if (term.Length == 0) return new SearchResultDto(new(), new());

        var titles = await (await VisibleTitlesAsync(ct))
            .Where(t => t.SearchText.Contains(term))
            .OrderByDescending(t => t.SearchText.StartsWith(term))
            .ThenByDescending(t => t.Popularity)
            .Take(24)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);

        var people = await _db.People.AsNoTracking()
            .Where(p => p.SearchName.Contains(term))
            .OrderByDescending(p => p.SearchName.StartsWith(term))
            .ThenByDescending(p => p.Popularity)
            .Take(12)
            .Select(p => new PersonCardDto(p.Id, p.Name, p.ProfileUrl, p.KnownForDepartment))
            .ToListAsync(ct);

        return new SearchResultDto(titles, people);
    }

    public async Task<List<GenreDto>> GetGenresAsync(CancellationToken ct)
    {
        var limit = await _current.GetMaturityLimitAsync(ct);
        return await _db.Genres.AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GenreDto(g.Id, g.Name, g.Slug,
                g.TitleGenres.Count(tg => limit == null || (tg.Title.AgeRatingRank != null && tg.Title.AgeRatingRank <= limit))))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<TitleCardDto>> GetGenreTitlesAsync(string slug, TitleQueryParameters p, CancellationToken ct)
    {
        var genre = await _db.Genres.AsNoTracking().FirstOrDefaultAsync(g => g.Slug == slug, ct)
            ?? throw new NotFoundException($"Gênero '{slug}' não encontrado.");
        p.GenreIds = genre.Id.ToString();
        return await ListAsync(p, ct);
    }

    public async Task<PersonDetailDto> GetPersonAsync(int id, CancellationToken ct)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException($"Pessoa com id {id} não encontrada.");

        // Biografia é buscada no TMDB sob demanda (no import só vem nome e foto)
        if (person.DetailsFetchedAt == null && _importer != null && await _importer.EnrichPersonAsync(person, ct))
            await _db.SaveChangesAsync(ct);

        var limit = await _current.GetMaturityLimitAsync(ct);
        var credits = await _db.Credits.AsNoTracking()
            .Where(c => c.PersonId == id)
            .Where(c => limit == null || (c.Title.AgeRatingRank != null && c.Title.AgeRatingRank <= limit))
            .Select(c => new { c.TitleId, c.Character, c.Job, c.Title.Popularity, c.Title.ReleaseDate })
            .ToListAsync(ct);

        var titleIds = credits.Select(c => c.TitleId).Distinct().ToList();
        var cards = await _db.Titles.AsNoTracking()
            .Where(t => titleIds.Contains(t.Id))
            .Select(TitleQueries.ToCard)
            .ToDictionaryAsync(t => t.Id, ct);

        var grouped = credits
            .GroupBy(c => c.TitleId)
            .Select(g => new
            {
                g.First().ReleaseDate,
                g.First().Popularity,
                Credit = new PersonCreditDto(cards[g.Key],
                    string.Join(" / ", g.Where(x => x.Character != null).Select(x => x.Character).Distinct()) is var ch && ch.Length > 0 ? ch : null,
                    string.Join(", ", g.Where(x => x.Job != null).Select(x => x.Job).Distinct()) is var job && job.Length > 0 ? job : null)
            })
            .ToList();

        return new PersonDetailDto
        {
            Id = person.Id,
            TmdbId = person.TmdbId,
            Name = person.Name,
            ProfileUrl = person.ProfileUrl,
            Biography = person.Biography,
            Birthday = person.Birthday,
            Deathday = person.Deathday,
            PlaceOfBirth = person.PlaceOfBirth,
            KnownForDepartment = person.KnownForDepartment,
            KnownFor = grouped.OrderByDescending(g => g.Popularity).Take(10).Select(g => g.Credit.Title).ToList(),
            Credits = grouped.OrderByDescending(g => g.ReleaseDate).Select(g => g.Credit).ToList()
        };
    }

    public async Task<CollectionDetailDto> GetCollectionAsync(int id, CancellationToken ct)
    {
        var collection = await _db.Collections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Coleção com id {id} não encontrada.");

        var titles = await (await VisibleTitlesAsync(ct))
            .Where(t => t.CollectionId == id)
            .OrderBy(t => t.ReleaseDate)
            .Select(TitleQueries.ToCard)
            .ToListAsync(ct);

        return new CollectionDetailDto
        {
            Id = collection.Id,
            Name = collection.Name,
            Overview = collection.Overview,
            PosterUrl = collection.PosterUrl,
            BackdropUrl = collection.BackdropUrl,
            Titles = titles
        };
    }

    public async Task<List<ProviderDto>> GetProvidersAsync(CancellationToken ct)
    {
        // Um título pode aparecer como stream e aluguel no mesmo provedor: conta títulos distintos
        var pairs = await _db.TitleProviders.AsNoTracking()
            .Select(tp => new { tp.ProviderId, tp.TitleId })
            .Distinct()
            .ToListAsync(ct);
        var counts = pairs.GroupBy(p => p.ProviderId).ToDictionary(g => g.Key, g => g.Count());

        var providers = await _db.WatchProviders.AsNoTracking().ToListAsync(ct);
        return providers
            .Select(p => new ProviderDto(p.Id, p.Name, p.LogoUrl, counts.GetValueOrDefault(p.Id)))
            .OrderByDescending(p => p.TitleCount)
            .ThenBy(p => p.Name)
            .ToList();
    }
}
