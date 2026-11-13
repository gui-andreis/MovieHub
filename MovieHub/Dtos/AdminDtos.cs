using System.ComponentModel.DataAnnotations;
using MovieHub.Models;

namespace MovieHub.Dtos;

public class TitleUpsertRequest
{
    [Required]
    public TitleType? Type { get; set; }

    [Required, StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)] public string? OriginalName { get; set; }
    [StringLength(500)] public string? Tagline { get; set; }
    [Required] public string Overview { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    [Range(1, 1000)] public int? RuntimeMinutes { get; set; }
    [Url] public string? PosterUrl { get; set; }
    [Url] public string? BackdropUrl { get; set; }
    [Url] public string? LogoUrl { get; set; }
    [StringLength(32)] public string? TrailerKey { get; set; }
    public string? AgeRating { get; set; }
    [Range(0, 10)] public double? VoteAverage { get; set; }
    [StringLength(8)] public string? OriginalLanguage { get; set; }
    [Range(0, 200)] public int? NumberOfSeasons { get; set; }
    [Range(0, 10000)] public int? NumberOfEpisodes { get; set; }
    public bool IsFeatured { get; set; }
    public List<int> GenreIds { get; set; } = new();
}

public class FeaturedRequest
{
    public bool IsFeatured { get; set; }
}

public class GenreRequest
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}

public class ImportRequest
{
    /// <summary>movie, series ou both.</summary>
    public string Type { get; set; } = "both";

    /// <summary>Páginas (20 títulos cada) por lista do TMDB.</summary>
    [Range(1, 25)]
    public int Pages { get; set; } = 5;
}

public record TitleCountsDto(int Movies, int Series, int Total, int Featured);

public record TopListedDto(TitleCardDto Title, int Count);

public record AdminStatsDto(
    TitleCountsDto Titles,
    int Users,
    int Profiles,
    int Reviews,
    int People,
    int Genres,
    int Seasons,
    int Episodes,
    int Providers,
    int Collections,
    List<TopListedDto> TopInMyList);
