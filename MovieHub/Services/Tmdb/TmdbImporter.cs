using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Models;

namespace MovieHub.Services.Tmdb;

public interface ITmdbImporter
{
    bool IsConfigured { get; }

    /// <summary>Importa as listas populares/aclamadas/lançamentos do TMDB.</summary>
    Task ImportCatalogAsync(ImportJob job, CancellationToken ct);

    /// <summary>Importa/atualiza um único título. Retorna o id local.</summary>
    Task<int> ImportTitleAsync(int tmdbId, TitleType type, CancellationToken ct);

    /// <summary>Preenche biografia/nascimento etc. de uma pessoa (não salva — o chamador salva).</summary>
    Task<bool> EnrichPersonAsync(Person person, CancellationToken ct);
}

public record ImportJob(bool Movies, bool Series, int MovieCount, int SeriesCount, string Source);

/// <summary>Dados brutos de um título já baixados do TMDB, prontos para persistir.</summary>
internal sealed class TitlePayload
{
    public TitleType Type { get; init; }
    public TmdbMovieDetail? Movie { get; init; }
    public TmdbTvDetail? Tv { get; init; }
    public List<TmdbSeasonDetail> Seasons { get; init; } = new();
    public TmdbCollection? Collection { get; set; }
    public TmdbTitleDetail Detail => (TmdbTitleDetail?)Movie ?? Tv!;
    public int TmdbId => Detail.Id;
}

public class TmdbImporter : ITmdbImporter
{
    // Gêneros de TV do TMDB que mapeamos para os gêneros de filme equivalentes (catálogo unificado)
    private static readonly Dictionary<int, int[]> TvGenreMap = new()
    {
        [10759] = new[] { 28, 12 },     // Action & Adventure → Ação, Aventura
        [10765] = new[] { 878, 14 },    // Sci-Fi & Fantasy → Ficção científica, Fantasia
        [10768] = new[] { 10752 },      // War & Politics → Guerra
        [10762] = new[] { 10751 }       // Kids → Família
    };

    // Programas que não fazem sentido num catálogo de streaming (jornal, talk show)
    private static readonly HashSet<int> ExcludedTvGenres = new() { 10763, 10767 };

    private static readonly Dictionary<string, int> CrewJobs = new()
    {
        ["Creator"] = 0,
        ["Director"] = 1,
        ["Screenplay"] = 2,
        ["Writer"] = 3,
        ["Novel"] = 4,
        ["Original Music Composer"] = 5
    };

    private readonly TmdbClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ImportStatusTracker _status;
    private readonly ILogger<TmdbImporter> _logger;
    private readonly SemaphoreSlim _persistLock = new(1, 1);

    public TmdbImporter(TmdbClient client, IServiceScopeFactory scopeFactory, ImportStatusTracker status, ILogger<TmdbImporter> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _status = status;
        _logger = logger;
    }

    public bool IsConfigured => _client.Options.IsConfigured;

    private TmdbOptions Options => _client.Options;

    // ------------------------------------------------------------------ catálogo

    public async Task ImportCatalogAsync(ImportJob job, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("TMDB não configurado (Tmdb__ReadAccessToken ausente) — importação ignorada.");
            _status.Fail("TMDB não configurado. Defina Tmdb__ReadAccessToken.");
            return;
        }

        var sw = Stopwatch.StartNew();
        _status.Start(job);
        _logger.LogInformation("Importação TMDB iniciada ({Source}): filmes={Movies} séries={Series}", job.Source, job.MovieCount, job.SeriesCount);

        try
        {
            await EnsureGenresAsync(ct);

            var targets = new List<(int Id, TitleType Type)>();
            if (job.Movies)
            {
                var ids = await CollectIdsAsync(new[] { "movie/popular", "movie/top_rated", "movie/now_playing" }, job.MovieCount, null, ct);
                var upcoming = await CollectIdsAsync(new[] { "movie/upcoming" }, 20, null, ct);
                targets.AddRange(ids.Concat(upcoming.Where(id => !ids.Contains(id))).Select(id => (id, TitleType.Movie)));
            }
            if (job.Series)
            {
                var ids = await CollectIdsAsync(new[] { "tv/popular", "tv/top_rated", "tv/on_the_air" }, job.SeriesCount, ExcludedTvGenres, ct);
                targets.AddRange(ids.Select(id => (id, TitleType.Series)));
            }

            _status.SetTotal(targets.Count);
            _logger.LogInformation("TMDB: {Count} títulos para importar", targets.Count);

            var collectionCache = new Dictionary<int, TmdbCollection?>();
            var collectionLock = new SemaphoreSlim(1, 1);

            await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Options.MaxConcurrency), CancellationToken = ct },
                async (target, token) =>
                {
                    try
                    {
                        var payload = await FetchAsync(target.Id, target.Type, token);
                        if (payload == null)
                        {
                            _status.Skipped();
                            return;
                        }

                        // Coleções (sagas): busca uma vez por coleção
                        var colRef = payload.Movie?.BelongsToCollection;
                        if (colRef != null)
                        {
                            await collectionLock.WaitAsync(token);
                            try
                            {
                                if (!collectionCache.TryGetValue(colRef.Id, out var col))
                                {
                                    col = await _client.GetCollectionAsync(colRef.Id, token)
                                          ?? new TmdbCollection { Id = colRef.Id, Name = colRef.Name, PosterPath = colRef.PosterPath, BackdropPath = colRef.BackdropPath };
                                    collectionCache[colRef.Id] = col;
                                }
                                payload.Collection = col;
                            }
                            finally { collectionLock.Release(); }
                        }

                        await PersistAsync(payload, token);
                        _status.Imported();
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _status.Failed(ex.Message);
                        _logger.LogWarning(ex, "Falha ao importar {Type} {TmdbId}", target.Type, target.Id);
                    }
                });

            await MarkFeaturedAsync(ct);
            _status.Complete($"Importação concluída em {sw.Elapsed.TotalSeconds:F0}s.");
            _logger.LogInformation("Importação TMDB concluída em {Elapsed}s: {@Status}", (int)sw.Elapsed.TotalSeconds, _status.Snapshot());
        }
        catch (OperationCanceledException)
        {
            _status.Fail("Importação cancelada.");
            throw;
        }
        catch (Exception ex)
        {
            _status.Fail(ex.Message);
            _logger.LogError(ex, "Importação TMDB falhou");
        }
    }

    private async Task<List<int>> CollectIdsAsync(string[] lists, int count, HashSet<int>? excludedGenres, CancellationToken ct)
    {
        var ids = new List<int>();
        var seen = new HashSet<int>();
        var pagesPerList = Math.Max(1, (int)Math.Ceiling(count / 20.0 / lists.Length * 1.6));

        for (var page = 1; page <= pagesPerList && ids.Count < count; page++)
        {
            foreach (var list in lists)
            {
                var result = await _client.GetListAsync(list, page, ct);
                if (result == null) continue;
                foreach (var item in result.Results)
                {
                    if (item.Adult || item.PosterPath == null) continue;
                    if (excludedGenres != null && item.GenreIds.Any(excludedGenres.Contains)) continue;
                    if (seen.Add(item.Id)) ids.Add(item.Id);
                }
            }
        }

        // Continua paginando o "popular" se ainda faltar título
        var extraPage = pagesPerList + 1;
        while (ids.Count < count && extraPage <= 50)
        {
            var result = await _client.GetListAsync(lists[0], extraPage++, ct);
            if (result == null || result.Results.Count == 0) break;
            foreach (var item in result.Results)
            {
                if (item.Adult || item.PosterPath == null) continue;
                if (excludedGenres != null && item.GenreIds.Any(excludedGenres.Contains)) continue;
                if (seen.Add(item.Id)) ids.Add(item.Id);
            }
        }

        return ids.Take(count).ToList();
    }

    private async Task EnsureGenresAsync(CancellationToken ct)
    {
        var movieGenres = (await _client.GetGenresAsync("movie", ct))?.Genres ?? new();
        var tvGenres = (await _client.GetGenresAsync("tv", ct))?.Genres ?? new();
        var all = movieGenres
            .Concat(tvGenres.Where(g => !TvGenreMap.ContainsKey(g.Id) && !ExcludedTvGenres.Contains(g.Id)))
            .GroupBy(g => g.Id)
            .Select(g => g.First())
            .ToList();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existing = await db.Genres.ToListAsync(ct);

        foreach (var g in all)
        {
            var name = Capitalize(g.Name);
            var genre = existing.FirstOrDefault(e => e.TmdbId == g.Id)
                        ?? existing.FirstOrDefault(e => e.TmdbId == null && TextNormalizer.Normalize(e.Name) == TextNormalizer.Normalize(name));
            if (genre == null)
            {
                genre = new Genre { TmdbId = g.Id };
                db.Genres.Add(genre);
                existing.Add(genre);
            }
            genre.TmdbId = g.Id;
            genre.Name = name;
            var slug = TextNormalizer.Slugify(name);
            if (existing.Any(e => e != genre && e.Slug == slug)) slug = $"{slug}-{g.Id}";
            genre.Slug = slug;
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Capitalize(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name[1..];

    // ------------------------------------------------------------------ download

    private async Task<TitlePayload?> FetchAsync(int tmdbId, TitleType type, CancellationToken ct)
    {
        if (type == TitleType.Movie)
        {
            var movie = await _client.GetMovieAsync(tmdbId, ct);
            if (movie == null || movie.Adult) return null;
            if (string.IsNullOrWhiteSpace(movie.Overview) || string.IsNullOrWhiteSpace(movie.Tagline))
            {
                var (overview, tagline) = await _client.GetFallbackTextAsync("movie", tmdbId, ct);
                if (string.IsNullOrWhiteSpace(movie.Overview)) movie.Overview = overview;
            }
            if (string.IsNullOrWhiteSpace(movie.Overview) || movie.PosterPath == null) return null;
            return new TitlePayload { Type = TitleType.Movie, Movie = movie };
        }

        var tv = await _client.GetTvAsync(tmdbId, ct);
        if (tv == null || tv.Adult) return null;
        if (string.IsNullOrWhiteSpace(tv.Overview))
        {
            var (overview, _) = await _client.GetFallbackTextAsync("tv", tmdbId, ct);
            tv.Overview = overview;
        }
        if (string.IsNullOrWhiteSpace(tv.Overview) || tv.PosterPath == null) return null;

        // Episódios: as temporadas mais recentes até o limite configurado (séries longas ficam só com o resumo)
        var seasonNumbers = tv.Seasons
            .Where(s => s.SeasonNumber > 0 && s.EpisodeCount > 0)
            .OrderBy(s => s.SeasonNumber)
            .Select(s => s.SeasonNumber)
            .TakeLast(Math.Max(1, Options.MaxSeasonsWithEpisodes))
            .ToList();

        var seasons = new List<TmdbSeasonDetail>();
        foreach (var n in seasonNumbers)
        {
            var season = await _client.GetSeasonAsync(tmdbId, n, ct);
            if (season != null) seasons.Add(season);
        }

        return new TitlePayload { Type = TitleType.Series, Tv = tv, Seasons = seasons };
    }

    // ------------------------------------------------------------------ persistência

    private async Task<int> PersistAsync(TitlePayload payload, CancellationToken ct)
    {
        await _persistLock.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await UpsertAsync(db, payload, ct);
        }
        finally
        {
            _persistLock.Release();
        }
    }

    private async Task<int> UpsertAsync(ApplicationDbContext db, TitlePayload payload, CancellationToken ct)
    {
        var d = payload.Detail;
        var title = await db.Titles
            .Include(t => t.TitleGenres)
            .Include(t => t.TitleKeywords)
            .Include(t => t.TitleProviders)
            .Include(t => t.Credits)
            .FirstOrDefaultAsync(t => t.TmdbId == d.Id && t.Type == payload.Type, ct);

        if (title == null)
        {
            title = new Title { TmdbId = d.Id, Type = payload.Type };
            db.Titles.Add(title);
        }

        // ---- campos básicos
        if (payload.Movie is { } m)
        {
            title.Name = m.Title;
            title.OriginalName = m.OriginalTitle;
            title.ReleaseDate = TmdbUrls.ParseDate(m.ReleaseDate);
            title.RuntimeMinutes = m.Runtime is > 0 ? m.Runtime : null;
            title.AgeRating = MovieCertification(m);
        }
        else if (payload.Tv is { } tv)
        {
            title.Name = tv.Name;
            title.OriginalName = tv.OriginalName;
            title.ReleaseDate = TmdbUrls.ParseDate(tv.FirstAirDate);
            var episodeRuntimes = payload.Seasons.SelectMany(s => s.Episodes).Where(e => e.Runtime is > 0).Select(e => e.Runtime!.Value).ToList();
            title.RuntimeMinutes = tv.EpisodeRunTime.FirstOrDefault() is var rt && rt > 0
                ? rt
                : episodeRuntimes.Count > 0 ? (int)Math.Round(episodeRuntimes.Average()) : null;
            title.NumberOfSeasons = tv.NumberOfSeasons;
            title.NumberOfEpisodes = tv.NumberOfEpisodes;
            title.AgeRating = TvCertification(tv);
        }

        title.Name = Truncate(title.Name, 300);
        title.OriginalName = Truncate(title.OriginalName, 300);
        title.Tagline = string.IsNullOrWhiteSpace(d.Tagline) ? null : Truncate(d.Tagline, 500);
        title.Overview = d.Overview?.Trim() ?? string.Empty;
        title.Year = title.ReleaseDate?.Year;
        title.PosterUrl = TmdbUrls.Poster(d.PosterPath);
        title.BackdropUrl = TmdbUrls.Backdrop(d.BackdropPath ?? d.Images?.Backdrops.OrderByDescending(b => b.VoteAverage).FirstOrDefault()?.FilePath);
        title.LogoUrl = TmdbUrls.Logo(PickLogo(d.Images));
        title.TrailerKey = PickTrailer(d.Videos);
        title.AgeRatingRank = AgeRatings.Rank(title.AgeRating);
        title.VoteAverage = Math.Round(d.VoteAverage, 1);
        title.VoteCount = d.VoteCount;
        title.Popularity = d.Popularity;
        title.OriginalLanguage = d.OriginalLanguage;
        title.Status = Truncate(d.Status, 50);
        title.SearchText = TextNormalizer.SearchText(title.Name, title.OriginalName);
        title.UpdatedAt = DateTime.UtcNow;

        // ---- coleção
        if (payload.Collection is { } col)
        {
            var collection = await db.Collections.FirstOrDefaultAsync(c => c.TmdbId == col.Id, ct);
            if (collection == null)
            {
                collection = new Collection { TmdbId = col.Id };
                db.Collections.Add(collection);
            }
            collection.Name = Truncate(col.Name, 300)!;
            collection.Overview = string.IsNullOrWhiteSpace(col.Overview) ? collection.Overview : col.Overview;
            collection.PosterUrl = TmdbUrls.Poster(col.PosterPath) ?? collection.PosterUrl;
            collection.BackdropUrl = TmdbUrls.Backdrop(col.BackdropPath) ?? collection.BackdropUrl;
            title.Collection = collection;
        }

        // ---- gêneros
        var genreTmdbIds = d.Genres.SelectMany(g => TvGenreMap.TryGetValue(g.Id, out var mapped) ? mapped : new[] { g.Id }).Distinct().ToList();
        var genreIds = await db.Genres.Where(g => g.TmdbId != null && genreTmdbIds.Contains(g.TmdbId.Value)).Select(g => g.Id).ToListAsync(ct);
        SyncJoin(title.TitleGenres, genreIds, x => x.GenreId, id => new TitleGenre { Title = title, GenreId = id });

        // ---- palavras-chave
        var tmdbKeywords = (d.Keywords?.All ?? Enumerable.Empty<TmdbKeyword>()).GroupBy(k => k.Id).Select(g => g.First()).Take(25).ToList();
        var keywordTmdbIds = tmdbKeywords.Select(k => k.Id).ToList();
        var keywords = await db.Keywords.Where(k => keywordTmdbIds.Contains(k.TmdbId)).ToListAsync(ct);
        foreach (var k in tmdbKeywords.Where(k => keywords.All(e => e.TmdbId != k.Id)))
        {
            var keyword = new Keyword { TmdbId = k.Id, Name = Truncate(k.Name, 150)! };
            db.Keywords.Add(keyword);
            keywords.Add(keyword);
        }
        await db.SaveChangesAsync(ct); // garante ids de keywords/coleção/título
        SyncJoin(title.TitleKeywords, keywords.Select(k => k.Id).ToList(), x => x.KeywordId, id => new TitleKeyword { TitleId = title.Id, KeywordId = id });

        // ---- onde assistir (BR)
        await SyncProvidersAsync(db, title, d.WatchProviders, ct);

        // ---- elenco e equipe
        await SyncCreditsAsync(db, title, payload, ct);

        // ---- temporadas / episódios
        if (payload.Tv is { } series)
            await SyncSeasonsAsync(db, title, series, payload.Seasons, ct);

        await db.SaveChangesAsync(ct);
        return title.Id;
    }

    private static void SyncJoin<T>(ICollection<T> current, List<int> desired, Func<T, int> key, Func<int, T> create)
    {
        foreach (var item in current.Where(c => !desired.Contains(key(c))).ToList())
            current.Remove(item);
        foreach (var id in desired.Where(id => current.All(c => key(c) != id)))
            current.Add(create(id));
    }

    private async Task SyncProvidersAsync(ApplicationDbContext db, Title title, TmdbWatchProviders? providers, CancellationToken ct)
    {
        var desired = new List<(TmdbProvider Provider, ProviderKind Kind)>();
        if (providers?.Results.TryGetValue(Options.Region, out var br) == true)
        {
            desired.AddRange((br.Flatrate ?? new()).Select(p => (p, ProviderKind.Stream)));
            desired.AddRange((br.Rent ?? new()).Select(p => (p, ProviderKind.Rent)));
            desired.AddRange((br.Buy ?? new()).Select(p => (p, ProviderKind.Buy)));
        }

        var tmdbIds = desired.Select(x => x.Provider.ProviderId).Distinct().ToList();
        var existing = await db.WatchProviders.Where(p => tmdbIds.Contains(p.TmdbId)).ToListAsync(ct);
        foreach (var p in desired.Select(x => x.Provider).GroupBy(p => p.ProviderId).Select(g => g.First()))
        {
            var provider = existing.FirstOrDefault(e => e.TmdbId == p.ProviderId);
            if (provider == null)
            {
                provider = new WatchProvider { TmdbId = p.ProviderId };
                db.WatchProviders.Add(provider);
                existing.Add(provider);
            }
            provider.Name = Truncate(p.ProviderName, 120)!;
            provider.LogoUrl = TmdbUrls.ProviderLogo(p.LogoPath);
            provider.DisplayPriority = p.DisplayPriority;
        }
        await db.SaveChangesAsync(ct);

        var wanted = desired
            .Select(x => (ProviderId: existing.First(e => e.TmdbId == x.Provider.ProviderId).Id, x.Kind))
            .Distinct()
            .ToList();

        foreach (var tp in title.TitleProviders.Where(tp => !wanted.Contains((tp.ProviderId, tp.Kind))).ToList())
            title.TitleProviders.Remove(tp);
        foreach (var w in wanted.Where(w => !title.TitleProviders.Any(tp => tp.ProviderId == w.ProviderId && tp.Kind == w.Kind)))
            title.TitleProviders.Add(new TitleProvider { TitleId = title.Id, ProviderId = w.ProviderId, Kind = w.Kind });
    }

    private static async Task SyncCreditsAsync(ApplicationDbContext db, Title title, TitlePayload payload, CancellationToken ct)
    {
        var d = payload.Detail;
        var cast = (d.Credits?.Cast ?? new()).OrderBy(c => c.Order).Take(20).ToList();

        var crew = (d.Credits?.Crew ?? new())
            .Where(c => c.Job != null && CrewJobs.ContainsKey(c.Job))
            .Select(c => (c.Id, c.Name, Job: c.Job!, c.ProfilePath, c.KnownForDepartment, c.Popularity))
            .ToList();
        if (payload.Tv != null)
            crew.InsertRange(0, payload.Tv.CreatedBy.Select(c => (c.Id, c.Name, Job: "Creator", c.ProfilePath, (string?)"Writing", 0d)));
        crew = crew
            .GroupBy(c => (c.Id, c.Job)).Select(g => g.First())
            .OrderBy(c => CrewJobs[c.Job])
            .Take(10)
            .ToList();

        // Pessoas: cria as que ainda não existem
        var personTmdbIds = cast.Select(c => c.Id).Concat(crew.Select(c => c.Id)).Distinct().ToList();
        var people = await db.People.Where(p => personTmdbIds.Contains(p.TmdbId)).ToDictionaryAsync(p => p.TmdbId, ct);

        void EnsurePerson(int id, string name, string? profilePath, string? department, double popularity)
        {
            if (!people.TryGetValue(id, out var person))
            {
                person = new Person { TmdbId = id };
                db.People.Add(person);
                people[id] = person;
            }
            person.Name = Truncate(name, 200)!;
            person.SearchName = Truncate(TextNormalizer.Normalize(name), 200)!;
            person.ProfileUrl = TmdbUrls.Profile(profilePath) ?? person.ProfileUrl;
            person.KnownForDepartment ??= Truncate(department, 60);
            person.Popularity = Math.Max(person.Popularity, popularity);
        }

        foreach (var c in cast) EnsurePerson(c.Id, c.Name, c.ProfilePath, c.KnownForDepartment ?? "Acting", c.Popularity);
        foreach (var c in crew) EnsurePerson(c.Id, c.Name, c.ProfilePath, c.KnownForDepartment, c.Popularity);
        await db.SaveChangesAsync(ct);

        foreach (var credit in title.Credits.ToList())
            db.Credits.Remove(credit);

        var order = 0;
        foreach (var c in cast)
            db.Credits.Add(new Credit
            {
                TitleId = title.Id, PersonId = people[c.Id].Id, Role = CreditRole.Cast,
                Character = Truncate(c.Character, 300), Order = order++
            });

        order = 0;
        foreach (var c in crew)
            db.Credits.Add(new Credit
            {
                TitleId = title.Id, PersonId = people[c.Id].Id, Role = CreditRole.Crew,
                Job = c.Job, Order = order++
            });
    }

    private static async Task SyncSeasonsAsync(ApplicationDbContext db, Title title, TmdbTvDetail tv, List<TmdbSeasonDetail> details, CancellationToken ct)
    {
        var seasons = await db.Seasons.Include(s => s.Episodes).Where(s => s.TitleId == title.Id).ToListAsync(ct);

        foreach (var s in tv.Seasons.Where(s => s.SeasonNumber > 0))
        {
            var season = seasons.FirstOrDefault(x => x.SeasonNumber == s.SeasonNumber);
            if (season == null)
            {
                season = new Season { TitleId = title.Id, SeasonNumber = s.SeasonNumber };
                db.Seasons.Add(season);
                seasons.Add(season);
            }
            season.Name = Truncate(string.IsNullOrWhiteSpace(s.Name) ? $"Temporada {s.SeasonNumber}" : s.Name, 200)!;
            season.Overview = string.IsNullOrWhiteSpace(s.Overview) ? season.Overview : s.Overview;
            season.PosterUrl = TmdbUrls.Poster(s.PosterPath) ?? season.PosterUrl;
            season.AirDate = TmdbUrls.ParseDate(s.AirDate);
            season.EpisodeCount = s.EpisodeCount;

            var detail = details.FirstOrDefault(x => x.SeasonNumber == s.SeasonNumber);
            if (detail == null) continue;

            foreach (var e in detail.Episodes)
            {
                var episode = season.Episodes.FirstOrDefault(x => x.EpisodeNumber == e.EpisodeNumber);
                if (episode == null)
                {
                    episode = new Episode { EpisodeNumber = e.EpisodeNumber };
                    season.Episodes.Add(episode);
                }
                episode.Name = Truncate(string.IsNullOrWhiteSpace(e.Name) ? $"Episódio {e.EpisodeNumber}" : e.Name, 300)!;
                episode.Overview = string.IsNullOrWhiteSpace(e.Overview) ? episode.Overview : e.Overview;
                episode.StillUrl = TmdbUrls.Still(e.StillPath) ?? episode.StillUrl;
                episode.RuntimeMinutes = e.Runtime is > 0 ? e.Runtime : title.RuntimeMinutes;
                episode.AirDate = TmdbUrls.ParseDate(e.AirDate);
            }
        }
    }

    private async Task MarkFeaturedAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Titles.AnyAsync(t => t.IsFeatured, ct)) return;

        // Primeiro import: destaca os 6 mais populares que têm backdrop, logo e trailer
        var featured = await db.Titles
            .Where(t => t.BackdropUrl != null && t.TrailerKey != null && t.LogoUrl != null && t.VoteAverage >= 6.5)
            .OrderByDescending(t => t.Popularity)
            .Take(6)
            .ToListAsync(ct);
        foreach (var t in featured) t.IsFeatured = true;
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ helpers de mapeamento

    private string? MovieCertification(TmdbMovieDetail movie)
    {
        string? From(string country) => movie.ReleaseDates?.Results
            .FirstOrDefault(r => r.Country == country)?.ReleaseDates
            .OrderBy(r => r.Type)
            .Select(r => AgeRatings.FromCertification(r.Certification))
            .FirstOrDefault(r => r != null);

        return From(Options.Region) ?? From("US");
    }

    private string? TvCertification(TmdbTvDetail tv)
    {
        string? From(string country) => AgeRatings.FromCertification(
            tv.ContentRatings?.Results.FirstOrDefault(r => r.Country == country)?.Rating);

        return From(Options.Region) ?? From("US");
    }

    private static string? PickLogo(TmdbImages? images)
    {
        if (images == null || images.Logos.Count == 0) return null;
        int Rank(string? lang) => lang switch { "pt" => 0, "en" => 1, null => 2, _ => 3 };
        return images.Logos
            .Where(l => !l.FilePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => Rank(l.Language))
            .ThenByDescending(l => l.VoteAverage)
            .FirstOrDefault()?.FilePath;
    }

    private static string? PickTrailer(TmdbVideos? videos)
    {
        if (videos == null) return null;
        int TypeRank(string? type) => type switch { "Trailer" => 0, "Teaser" => 1, _ => 2 };
        int LangRank(string? lang) => lang switch { "pt" => 0, "en" => 1, _ => 2 };
        return videos.Results
            .Where(v => string.Equals(v.Site, "YouTube", StringComparison.OrdinalIgnoreCase) && TypeRank(v.Type) < 2)
            .OrderBy(v => TypeRank(v.Type))
            .ThenBy(v => LangRank(v.Language))
            .ThenByDescending(v => v.Official)
            .FirstOrDefault()?.Key;
    }

    private static string? Truncate(string? value, int max)
        => value == null ? null : value.Length <= max ? value : value[..max];

    // ------------------------------------------------------------------ títulos avulsos e pessoas

    public async Task<int> ImportTitleAsync(int tmdbId, TitleType type, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new Exceptions.BadRequestException("TMDB não configurado. Defina Tmdb__ReadAccessToken.");

        await EnsureGenresIfEmptyAsync(ct);
        var payload = await FetchAsync(tmdbId, type, ct)
            ?? throw new Exceptions.NotFoundException($"Título {tmdbId} não encontrado no TMDB (ou sem sinopse/pôster).");

        if (payload.Movie?.BelongsToCollection is { } colRef)
            payload.Collection = await _client.GetCollectionAsync(colRef.Id, ct)
                                 ?? new TmdbCollection { Id = colRef.Id, Name = colRef.Name, PosterPath = colRef.PosterPath, BackdropPath = colRef.BackdropPath };

        return await PersistAsync(payload, ct);
    }

    private async Task EnsureGenresIfEmptyAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Genres.AnyAsync(g => g.TmdbId != null, ct))
            await EnsureGenresAsync(ct);
    }

    public async Task<bool> EnrichPersonAsync(Person person, CancellationToken ct)
    {
        if (!IsConfigured) return false;
        try
        {
            var p = await _client.GetPersonAsync(person.TmdbId, ct);
            if (p == null) return false;
            person.Biography = string.IsNullOrWhiteSpace(p.Biography) ? null : p.Biography;
            person.Birthday = TmdbUrls.ParseDate(p.Birthday);
            person.Deathday = TmdbUrls.ParseDate(p.Deathday);
            person.PlaceOfBirth = Truncate(p.PlaceOfBirth, 300);
            person.KnownForDepartment = Truncate(p.KnownForDepartment, 60) ?? person.KnownForDepartment;
            person.ProfileUrl = TmdbUrls.Profile(p.ProfilePath) ?? person.ProfileUrl;
            person.Popularity = p.Popularity;
            person.DetailsFetchedAt = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível buscar detalhes da pessoa {TmdbId}", person.TmdbId);
            return false;
        }
    }
}
