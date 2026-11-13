using MovieHub.Models;

namespace MovieHub.Dtos;

/// <summary>Card de título usado em linhas, grids, busca e chat.</summary>
public class TitleCardDto
{
    public int Id { get; set; }
    public TitleType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? AgeRating { get; set; }
    public double VoteAverage { get; set; }
    public int? RuntimeMinutes { get; set; }
    public int? NumberOfSeasons { get; set; }
    public List<string> Genres { get; set; } = new();
    public string? TrailerKey { get; set; }
    public string Overview { get; set; } = string.Empty;
}

public class HeroDto : TitleCardDto
{
    public string? Tagline { get; set; }
    public string? FullOverview { get; set; }
}

public record GenreRefDto(int Id, string Name, string Slug);
public record CastDto(int PersonId, string Name, string? Character, string? ProfileUrl);
public record CrewDto(int PersonId, string Name, string? Job, string? ProfileUrl);
public record ProviderRefDto(int Id, string Name, string? LogoUrl, ProviderKind Kind);
public record SeasonSummaryDto(int Id, int SeasonNumber, string Name, int EpisodeCount, string? PosterUrl, DateOnly? AirDate);
public record CollectionRefDto(int Id, string Name, string? PosterUrl);
public record RatingSummaryDto(double Average, int Count);

public record ProgressDto(int? EpisodeId, int PositionSeconds, int DurationSeconds, int Percent, bool Completed);

public record MyReviewRefDto(int Id, int Rating, string? Comment, bool ContainsSpoilers, DateTime CreatedAt, DateTime? UpdatedAt);

public class TitleMeDto
{
    public bool InMyList { get; set; }
    public ReactionValue? Reaction { get; set; }
    public ProgressDto? Progress { get; set; }
    public MyReviewRefDto? Review { get; set; }
}

public class TitleDetailDto
{
    public int Id { get; set; }
    public int? TmdbId { get; set; }
    public TitleType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public string? Tagline { get; set; }
    public string Overview { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public int? Year { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? TrailerKey { get; set; }
    public string? AgeRating { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public double Popularity { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? Status { get; set; }
    public int? NumberOfSeasons { get; set; }
    public int? NumberOfEpisodes { get; set; }
    public bool IsFeatured { get; set; }
    public List<GenreRefDto> Genres { get; set; } = new();
    public List<CastDto> Cast { get; set; } = new();
    public List<CrewDto> Crew { get; set; } = new();
    public List<ProviderRefDto> Providers { get; set; } = new();
    public List<SeasonSummaryDto> Seasons { get; set; } = new();
    public CollectionRefDto? Collection { get; set; }
    public RatingSummaryDto Rating { get; set; } = new(0, 0);
    public TitleMeDto? Me { get; set; }
}

public class EpisodeDto
{
    public int Id { get; set; }
    public int EpisodeNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? StillUrl { get; set; }
    public int? RuntimeMinutes { get; set; }
    public DateOnly? AirDate { get; set; }
    public ProgressDto? Progress { get; set; }
}

public class SeasonDetailDto
{
    public int Id { get; set; }
    public int TitleId { get; set; }
    public int SeasonNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public DateOnly? AirDate { get; set; }
    public int EpisodeCount { get; set; }
    public List<EpisodeDto> Episodes { get; set; } = new();
}

public record HomeRowDto(string Key, string Title, string Kind, List<TitleCardDto> Items);

public record HomeDto(HeroDto? Hero, List<HomeRowDto> Rows);

public record PersonCardDto(int Id, string Name, string? ProfileUrl, string? KnownForDepartment);

public record SearchResultDto(List<TitleCardDto> Titles, List<PersonCardDto> People);

public record GenreDto(int Id, string Name, string Slug, int TitleCount);

public record ProviderDto(int Id, string Name, string? LogoUrl, int TitleCount);

public record PersonCreditDto(TitleCardDto Title, string? Character, string? Job);

public class PersonDetailDto
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
    public string? Biography { get; set; }
    public DateOnly? Birthday { get; set; }
    public DateOnly? Deathday { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? KnownForDepartment { get; set; }
    public List<TitleCardDto> KnownFor { get; set; } = new();
    public List<PersonCreditDto> Credits { get; set; } = new();
}

public class CollectionDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public List<TitleCardDto> Titles { get; set; } = new();
}
