namespace MovieHub.Models;

/// <summary>Filme ou série do catálogo.</summary>
public class Title
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

    /// <summary>Classificação indicativa brasileira: L, 10, 12, 14, 16, 18.</summary>
    public string? AgeRating { get; set; }

    /// <summary>Valor numérico da classificação (L = 0) usado nos filtros de maturidade.</summary>
    public int? AgeRatingRank { get; set; }

    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public double Popularity { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? Status { get; set; }
    public int? NumberOfSeasons { get; set; }
    public int? NumberOfEpisodes { get; set; }
    public bool IsFeatured { get; set; }

    /// <summary>Nome + nome original normalizados (minúsculo, sem acento) para busca.</summary>
    public string SearchText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    public ICollection<TitleGenre> TitleGenres { get; set; } = new List<TitleGenre>();
    public ICollection<TitleKeyword> TitleKeywords { get; set; } = new List<TitleKeyword>();
    public ICollection<TitleProvider> TitleProviders { get; set; } = new List<TitleProvider>();
    public ICollection<Credit> Credits { get; set; } = new List<Credit>();
    public ICollection<Season> Seasons { get; set; } = new List<Season>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public class Genre
{
    public int Id { get; set; }
    public int? TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ICollection<TitleGenre> TitleGenres { get; set; } = new List<TitleGenre>();
}

public class TitleGenre
{
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}

public class Keyword
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<TitleKeyword> TitleKeywords { get; set; } = new List<TitleKeyword>();
}

public class TitleKeyword
{
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public int KeywordId { get; set; }
    public Keyword Keyword { get; set; } = null!;
}

public class WatchProvider
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int DisplayPriority { get; set; }
    public ICollection<TitleProvider> TitleProviders { get; set; } = new List<TitleProvider>();
}

public class TitleProvider
{
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public int ProviderId { get; set; }
    public WatchProvider Provider { get; set; } = null!;
    public ProviderKind Kind { get; set; }
}

/// <summary>Saga / coleção de filmes (ex.: "Duna - Coleção").</summary>
public class Collection
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public ICollection<Title> Titles { get; set; } = new List<Title>();
}

public class Person
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SearchName { get; set; } = string.Empty;
    public string? ProfileUrl { get; set; }
    public string? Biography { get; set; }
    public DateOnly? Birthday { get; set; }
    public DateOnly? Deathday { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? KnownForDepartment { get; set; }
    public double Popularity { get; set; }

    /// <summary>Quando a biografia completa foi buscada no TMDB (busca preguiçosa no primeiro acesso).</summary>
    public DateTime? DetailsFetchedAt { get; set; }

    public ICollection<Credit> Credits { get; set; } = new List<Credit>();
}

public class Credit
{
    public int Id { get; set; }
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public CreditRole Role { get; set; }
    public string? Character { get; set; }
    public string? Job { get; set; }
    public int Order { get; set; }
}

public class Season
{
    public int Id { get; set; }
    public int TitleId { get; set; }
    public Title Title { get; set; } = null!;
    public int SeasonNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public DateOnly? AirDate { get; set; }
    public int EpisodeCount { get; set; }
    public ICollection<Episode> Episodes { get; set; } = new List<Episode>();
}

public class Episode
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;
    public int EpisodeNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? StillUrl { get; set; }
    public int? RuntimeMinutes { get; set; }
    public DateOnly? AirDate { get; set; }
}
