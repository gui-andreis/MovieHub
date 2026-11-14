using System.Linq.Expressions;
using MovieHub.Common;
using MovieHub.Dtos;
using MovieHub.Models;

namespace MovieHub.Services.Catalog;

/// <summary>Projeções e filtros reutilizáveis (traduzidos para SQL pelo EF).</summary>
public static class TitleQueries
{
    public const int CardOverviewLength = 200;

    public static readonly Expression<Func<Title, TitleCardDto>> ToCard = t => new TitleCardDto
    {
        Id = t.Id,
        Type = t.Type,
        Name = t.Name,
        Year = t.Year,
        ReleaseDate = t.ReleaseDate,
        IsFeatured = t.IsFeatured,
        PosterUrl = t.PosterUrl,
        BackdropUrl = t.BackdropUrl,
        LogoUrl = t.LogoUrl,
        AgeRating = t.AgeRating,
        VoteAverage = t.VoteAverage,
        RuntimeMinutes = t.RuntimeMinutes,
        NumberOfSeasons = t.NumberOfSeasons,
        Genres = t.TitleGenres.OrderBy(g => g.Genre.Name).Select(g => g.Genre.Name).ToList(),
        TrailerKey = t.TrailerKey,
        Overview = t.Overview.Length > CardOverviewLength
            ? t.Overview.Substring(0, CardOverviewLength - 1) + "…"
            : t.Overview
    };

    public static readonly Expression<Func<Title, HeroDto>> ToHero = t => new HeroDto
    {
        Id = t.Id,
        Type = t.Type,
        Name = t.Name,
        Year = t.Year,
        ReleaseDate = t.ReleaseDate,
        IsFeatured = t.IsFeatured,
        PosterUrl = t.PosterUrl,
        BackdropUrl = t.BackdropUrl,
        LogoUrl = t.LogoUrl,
        AgeRating = t.AgeRating,
        VoteAverage = t.VoteAverage,
        RuntimeMinutes = t.RuntimeMinutes,
        NumberOfSeasons = t.NumberOfSeasons,
        Genres = t.TitleGenres.OrderBy(g => g.Genre.Name).Select(g => g.Genre.Name).ToList(),
        TrailerKey = t.TrailerKey,
        Overview = t.Overview.Length > CardOverviewLength
            ? t.Overview.Substring(0, CardOverviewLength - 1) + "…"
            : t.Overview,
        Tagline = t.Tagline,
        FullOverview = t.Overview
    };

    /// <summary>Aplica o limite de classificação do perfil (null = sem restrição).</summary>
    public static IQueryable<Title> ForMaturity(this IQueryable<Title> query, int? limit)
        => limit == null ? query : query.Where(t => t.AgeRatingRank != null && t.AgeRatingRank <= limit);

    public static IQueryable<Title> OfType(this IQueryable<Title> query, TitleType? type)
        => type == null ? query : query.Where(t => t.Type == type);

    public static TitleType? ParseType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "movie" or "movies" or "filme" or "filmes" => TitleType.Movie,
        "series" or "serie" or "tv" or "show" => TitleType.Series,
        _ => null
    };

    public static List<int> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }

    public static IQueryable<Title> Search(this IQueryable<Title> query, string? q)
    {
        var term = TextNormalizer.Normalize(q);
        return term.Length == 0 ? query : query.Where(t => t.SearchText.Contains(term));
    }

    public static IQueryable<Title> ApplySort(this IQueryable<Title> query, string? sort) => sort?.ToLowerInvariant() switch
    {
        "rating" => query.OrderByDescending(t => t.VoteAverage).ThenByDescending(t => t.VoteCount),
        "release_desc" => query.OrderByDescending(t => t.ReleaseDate).ThenByDescending(t => t.Popularity),
        "release_asc" => query.OrderBy(t => t.ReleaseDate).ThenByDescending(t => t.Popularity),
        "name" => query.OrderBy(t => t.Name),
        "added" => query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id),
        _ => query.OrderByDescending(t => t.Popularity).ThenBy(t => t.Id)
    };

    public static int Percent(int position, int duration)
        => duration <= 0 ? 0 : Math.Clamp((int)Math.Round(position * 100.0 / duration), 0, 100);
}

public class TitleQueryParameters
{
    public string? Q { get; set; }
    public string? Type { get; set; }
    public string? GenreIds { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public double? MinRating { get; set; }
    public string? ProviderIds { get; set; }
    public string? MaxAgeRating { get; set; }
    public string? Language { get; set; }
    public int? MinRuntime { get; set; }
    public int? MaxRuntime { get; set; }

    /// <summary>popularity (padrão) | rating | release_desc | release_asc | name | added</summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 20;
        if (PageSize > 50) PageSize = 50;
    }
}

public class ReviewQueryParameters
{
    /// <summary>newest (padrão) | oldest | rating_desc | rating_asc</summary>
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 10;
        if (PageSize > 50) PageSize = 50;
    }
}
