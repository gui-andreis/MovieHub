using System.Text.Json.Serialization;

namespace MovieHub.Services.Tmdb;

// Modelos mínimos das respostas do TMDB (snake_case via JsonNamingPolicy.SnakeCaseLower)

public class TmdbPage<T>
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public List<T> Results { get; set; } = new();
}

public class TmdbListItem
{
    public int Id { get; set; }
    public bool Adult { get; set; }
    public double Popularity { get; set; }
    public List<int> GenreIds { get; set; } = new();
    public string? PosterPath { get; set; }
}

public class TmdbGenre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TmdbGenreList
{
    public List<TmdbGenre> Genres { get; set; } = new();
}

public class TmdbCast
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Character { get; set; }
    public int Order { get; set; }
    public string? ProfilePath { get; set; }
    public string? KnownForDepartment { get; set; }
    public double Popularity { get; set; }
}

public class TmdbCrew
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Job { get; set; }
    public string? Department { get; set; }
    public string? ProfilePath { get; set; }
    public string? KnownForDepartment { get; set; }
    public double Popularity { get; set; }
}

public class TmdbCredits
{
    public List<TmdbCast> Cast { get; set; } = new();
    public List<TmdbCrew> Crew { get; set; } = new();
}

public class TmdbVideo
{
    public string Key { get; set; } = string.Empty;
    public string? Site { get; set; }
    public string? Type { get; set; }
    [JsonPropertyName("iso_639_1")] public string? Language { get; set; }
    public bool Official { get; set; }
}

public class TmdbVideos
{
    public List<TmdbVideo> Results { get; set; } = new();
}

public class TmdbImage
{
    public string FilePath { get; set; } = string.Empty;
    [JsonPropertyName("iso_639_1")] public string? Language { get; set; }
    public double VoteAverage { get; set; }
    public int Width { get; set; }
}

public class TmdbImages
{
    public List<TmdbImage> Logos { get; set; } = new();
    public List<TmdbImage> Backdrops { get; set; } = new();
}

public class TmdbReleaseDate
{
    public string? Certification { get; set; }
    public int Type { get; set; }
}

public class TmdbCountryReleases
{
    [JsonPropertyName("iso_3166_1")] public string Country { get; set; } = string.Empty;
    public List<TmdbReleaseDate> ReleaseDates { get; set; } = new();
}

public class TmdbReleaseDates
{
    public List<TmdbCountryReleases> Results { get; set; } = new();
}

public class TmdbContentRating
{
    [JsonPropertyName("iso_3166_1")] public string Country { get; set; } = string.Empty;
    public string? Rating { get; set; }
}

public class TmdbContentRatings
{
    public List<TmdbContentRating> Results { get; set; } = new();
}

public class TmdbKeyword
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TmdbKeywords
{
    // Filmes usam "keywords", séries usam "results"
    public List<TmdbKeyword>? Keywords { get; set; }
    public List<TmdbKeyword>? Results { get; set; }
    public IEnumerable<TmdbKeyword> All => (Keywords ?? new()).Concat(Results ?? new());
}

public class TmdbProvider
{
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public int DisplayPriority { get; set; }
}

public class TmdbCountryProviders
{
    public List<TmdbProvider>? Flatrate { get; set; }
    public List<TmdbProvider>? Rent { get; set; }
    public List<TmdbProvider>? Buy { get; set; }
}

public class TmdbWatchProviders
{
    public Dictionary<string, TmdbCountryProviders> Results { get; set; } = new();
}

public class TmdbCollectionRef
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
}

public class TmdbCollection : TmdbCollectionRef
{
    public string? Overview { get; set; }
}

/// <summary>Campos comuns entre filme e série.</summary>
public abstract class TmdbTitleDetail
{
    public int Id { get; set; }
    public bool Adult { get; set; }
    public string? Tagline { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public double Popularity { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? Status { get; set; }
    public List<TmdbGenre> Genres { get; set; } = new();
    public TmdbCredits? Credits { get; set; }
    public TmdbVideos? Videos { get; set; }
    public TmdbImages? Images { get; set; }
    public TmdbKeywords? Keywords { get; set; }
    [JsonPropertyName("watch/providers")] public TmdbWatchProviders? WatchProviders { get; set; }
}

public class TmdbMovieDetail : TmdbTitleDetail
{
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public string? ReleaseDate { get; set; }
    public int? Runtime { get; set; }
    public TmdbCollectionRef? BelongsToCollection { get; set; }
    public TmdbReleaseDates? ReleaseDates { get; set; }
}

public class TmdbCreator
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ProfilePath { get; set; }
}

public class TmdbSeasonRef
{
    public int Id { get; set; }
    public int SeasonNumber { get; set; }
    public string? Name { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? AirDate { get; set; }
    public int EpisodeCount { get; set; }
}

public class TmdbTvDetail : TmdbTitleDetail
{
    public string Name { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public string? FirstAirDate { get; set; }
    public List<int> EpisodeRunTime { get; set; } = new();
    public int? NumberOfSeasons { get; set; }
    public int? NumberOfEpisodes { get; set; }
    public List<TmdbCreator> CreatedBy { get; set; } = new();
    public List<TmdbSeasonRef> Seasons { get; set; } = new();
    public TmdbContentRatings? ContentRatings { get; set; }
}

public class TmdbEpisode
{
    public int EpisodeNumber { get; set; }
    public string? Name { get; set; }
    public string? Overview { get; set; }
    public string? StillPath { get; set; }
    public int? Runtime { get; set; }
    public string? AirDate { get; set; }
}

public class TmdbSeasonDetail
{
    public int SeasonNumber { get; set; }
    public string? Name { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? AirDate { get; set; }
    public List<TmdbEpisode> Episodes { get; set; } = new();
}

public class TmdbPerson
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public string? Birthday { get; set; }
    public string? Deathday { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? KnownForDepartment { get; set; }
    public string? ProfilePath { get; set; }
    public double Popularity { get; set; }
}
