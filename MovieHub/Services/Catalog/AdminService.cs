using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Tmdb;

namespace MovieHub.Services.Catalog;

public interface IAdminService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken ct);
    ImportStatusDto StartImport(ImportRequest request);
    ImportStatusDto GetImportStatus();
    Task<TitleDetailDto> ImportTitleAsync(int tmdbId, string? type, CancellationToken ct);
    Task<TitleDetailDto> CreateTitleAsync(TitleUpsertRequest request, CancellationToken ct);
    Task<TitleDetailDto> UpdateTitleAsync(int id, TitleUpsertRequest request, CancellationToken ct);
    Task DeleteTitleAsync(int id, CancellationToken ct);
    Task<TitleCardDto> SetFeaturedAsync(int id, bool featured, CancellationToken ct);
    Task<GenreDto> CreateGenreAsync(GenreRequest request, CancellationToken ct);
    Task<GenreDto> UpdateGenreAsync(int id, GenreRequest request, CancellationToken ct);
    Task DeleteGenreAsync(int id, CancellationToken ct);
}

public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICatalogService _catalog;
    private readonly ITmdbImporter _importer;
    private readonly ImportQueue _queue;
    private readonly ImportStatusTracker _status;
    private readonly IOutputCacheStore _cache;

    public AdminService(ApplicationDbContext db, ICatalogService catalog, ITmdbImporter importer,
        ImportQueue queue, ImportStatusTracker status, IOutputCacheStore cache)
    {
        _db = db;
        _catalog = catalog;
        _importer = importer;
        _queue = queue;
        _status = status;
        _cache = cache;
    }

    private Task InvalidateCatalogAsync() => _cache.EvictByTagAsync(CacheTags.Catalog, CancellationToken.None).AsTask();

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var movies = await _db.Titles.CountAsync(t => t.Type == TitleType.Movie, ct);
        var series = await _db.Titles.CountAsync(t => t.Type == TitleType.Series, ct);
        var featured = await _db.Titles.CountAsync(t => t.IsFeatured, ct);

        var top = await _db.MyListItems.AsNoTracking()
            .GroupBy(m => m.TitleId)
            .Select(g => new { TitleId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);
        var ids = top.Select(t => t.TitleId).ToList();
        var cards = await _db.Titles.AsNoTracking().Where(t => ids.Contains(t.Id))
            .Select(TitleQueries.ToCard).ToDictionaryAsync(t => t.Id, ct);

        return new AdminStatsDto(
            new TitleCountsDto(movies, series, movies + series, featured),
            Users: await _db.Users.CountAsync(ct),
            Profiles: await _db.Profiles.CountAsync(ct),
            Reviews: await _db.Reviews.CountAsync(ct),
            People: await _db.People.CountAsync(ct),
            Genres: await _db.Genres.CountAsync(ct),
            Seasons: await _db.Seasons.CountAsync(ct),
            Episodes: await _db.Episodes.CountAsync(ct),
            Providers: await _db.WatchProviders.CountAsync(ct),
            Collections: await _db.Collections.CountAsync(ct),
            TopInMyList: top.Where(t => cards.ContainsKey(t.TitleId)).Select(t => new TopListedDto(cards[t.TitleId], t.Count)).ToList());
    }

    public ImportStatusDto StartImport(ImportRequest request)
    {
        if (!_importer.IsConfigured)
            throw new BadRequestException("TMDB não configurado. Defina a variável Tmdb__ReadAccessToken.");
        if (_status.IsRunning)
            throw new ConflictException("Já existe uma importação em andamento.");

        var type = request.Type?.ToLowerInvariant() ?? "both";
        var movies = type is "movie" or "both";
        var series = type is "series" or "both";
        if (!movies && !series)
            throw new BadRequestException("type deve ser movie, series ou both.");

        var count = request.Pages * 20 * 3;
        _status.Queue("admin");
        if (!_queue.TryEnqueue(new ImportJob(movies, series, count, count, "admin")))
            throw new ConflictException("Fila de importação cheia, tente novamente em instantes.");

        return _status.Snapshot();
    }

    public ImportStatusDto GetImportStatus() => _status.Snapshot();

    public async Task<TitleDetailDto> ImportTitleAsync(int tmdbId, string? type, CancellationToken ct)
    {
        var titleType = TitleQueries.ParseType(type) ?? TitleType.Movie;
        var id = await _importer.ImportTitleAsync(tmdbId, titleType, ct);
        await InvalidateCatalogAsync();
        return await _catalog.GetDetailAsync(id, ct);
    }

    public async Task<TitleDetailDto> CreateTitleAsync(TitleUpsertRequest request, CancellationToken ct)
    {
        var title = new Title();
        await ApplyAsync(title, request, ct);
        _db.Titles.Add(title);
        await _db.SaveChangesAsync(ct);
        await InvalidateCatalogAsync();
        return await _catalog.GetDetailAsync(title.Id, ct);
    }

    public async Task<TitleDetailDto> UpdateTitleAsync(int id, TitleUpsertRequest request, CancellationToken ct)
    {
        var title = await _db.Titles.Include(t => t.TitleGenres).FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Título com id {id} não encontrado.");
        await ApplyAsync(title, request, ct);
        title.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await InvalidateCatalogAsync();
        return await _catalog.GetDetailAsync(id, ct);
    }

    private async Task ApplyAsync(Title title, TitleUpsertRequest r, CancellationToken ct)
    {
        if (r.AgeRating != null && !AgeRatings.IsValid(r.AgeRating))
            throw new BadRequestException("ageRating deve ser L, 10, 12, 14, 16 ou 18.");

        var genreIds = r.GenreIds.Distinct().ToList();
        var validGenres = await _db.Genres.Where(g => genreIds.Contains(g.Id)).Select(g => g.Id).ToListAsync(ct);
        if (validGenres.Count != genreIds.Count)
            throw new BadRequestException("Um ou mais gêneros informados não existem.");

        title.Type = r.Type!.Value;
        title.Name = r.Name.Trim();
        title.OriginalName = r.OriginalName;
        title.Tagline = r.Tagline;
        title.Overview = r.Overview.Trim();
        title.ReleaseDate = r.ReleaseDate;
        title.Year = r.ReleaseDate?.Year;
        title.RuntimeMinutes = r.RuntimeMinutes;
        title.PosterUrl = r.PosterUrl;
        title.BackdropUrl = r.BackdropUrl;
        title.LogoUrl = r.LogoUrl;
        title.TrailerKey = r.TrailerKey;
        title.AgeRating = r.AgeRating;
        title.AgeRatingRank = AgeRatings.Rank(r.AgeRating);
        title.VoteAverage = r.VoteAverage ?? title.VoteAverage;
        title.OriginalLanguage = r.OriginalLanguage;
        title.NumberOfSeasons = r.Type == TitleType.Series ? r.NumberOfSeasons : null;
        title.NumberOfEpisodes = r.Type == TitleType.Series ? r.NumberOfEpisodes : null;
        title.IsFeatured = r.IsFeatured;
        title.SearchText = TextNormalizer.SearchText(title.Name, title.OriginalName);

        foreach (var tg in title.TitleGenres.Where(tg => !validGenres.Contains(tg.GenreId)).ToList())
            title.TitleGenres.Remove(tg);
        foreach (var gid in validGenres.Where(gid => title.TitleGenres.All(tg => tg.GenreId != gid)))
            title.TitleGenres.Add(new TitleGenre { Title = title, GenreId = gid });
    }

    public async Task DeleteTitleAsync(int id, CancellationToken ct)
    {
        var title = await _db.Titles.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Título com id {id} não encontrado.");
        _db.Titles.Remove(title);
        await _db.SaveChangesAsync(ct);
        await InvalidateCatalogAsync();
    }

    public async Task<TitleCardDto> SetFeaturedAsync(int id, bool featured, CancellationToken ct)
    {
        var title = await _db.Titles.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Título com id {id} não encontrado.");
        title.IsFeatured = featured;
        await _db.SaveChangesAsync(ct);
        await InvalidateCatalogAsync();
        return await _db.Titles.AsNoTracking().Where(t => t.Id == id).Select(TitleQueries.ToCard).FirstAsync(ct);
    }

    public async Task<GenreDto> CreateGenreAsync(GenreRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        var slug = TextNormalizer.Slugify(name);
        if (await _db.Genres.AnyAsync(g => g.Slug == slug, ct))
            throw new ConflictException($"Gênero '{name}' já existe.");

        var genre = new Genre { Name = name, Slug = slug };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync(ct);
        await InvalidateCatalogAsync();
        return new GenreDto(genre.Id, genre.Name, genre.Slug, 0);
    }

    public async Task<GenreDto> UpdateGenreAsync(int id, GenreRequest request, CancellationToken ct)
    {
        var genre = await _db.Genres.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Gênero com id {id} não encontrado.");
        var name = request.Name.Trim();
        var slug = TextNormalizer.Slugify(name);
        if (await _db.Genres.AnyAsync(g => g.Slug == slug && g.Id != id, ct))
            throw new ConflictException($"Gênero '{name}' já existe.");

        genre.Name = name;
        genre.Slug = slug;
        await _db.SaveChangesAsync(ct);
        await InvalidateCatalogAsync();
        var count = await _db.TitleGenres.CountAsync(tg => tg.GenreId == id, ct);
        return new GenreDto(genre.Id, genre.Name, genre.Slug, count);
    }

    public async Task DeleteGenreAsync(int id, CancellationToken ct)
    {
        var genre = await _db.Genres.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException($"Gênero com id {id} não encontrado.");
        _db.Genres.Remove(genre);
        await _db.SaveChangesAsync(ct);
        await InvalidateCatalogAsync();
    }
}
