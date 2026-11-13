using Microsoft.EntityFrameworkCore;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services;

namespace MovieHub.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // banco isolado por teste
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

/// <summary>ICurrentProfile controlável nos testes.</summary>
public class FakeCurrentProfile : ICurrentProfile
{
    public string? UserId { get; set; }
    public Profile? Profile { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsAuthenticated => UserId != null;

    public Task<Profile?> GetProfileAsync(CancellationToken ct = default) => Task.FromResult(Profile);

    public Task<Profile> RequireProfileAsync(CancellationToken ct = default)
        => Profile != null ? Task.FromResult(Profile) : throw new BadRequestException("Perfil obrigatório.");

    public Task<int?> GetMaturityLimitAsync(CancellationToken ct = default)
        => Task.FromResult(Profile == null ? null : AgeRatings.EffectiveLimit(Profile.MaturityLimit, Profile.IsKids));

    public string RequireUserId() => UserId ?? throw new UnauthorizedException("Autenticação necessária.");
}

/// <summary>Monta um catálogo pequeno e previsível.</summary>
public static class Seed
{
    public static Genre Action = null!, Drama = null!, Animation = null!;

    public static Title NewTitle(string name, TitleType type = TitleType.Movie, string? ageRating = "14",
        double vote = 7, double popularity = 10, int year = 2020, string? original = null)
        => new()
        {
            Name = name,
            OriginalName = original,
            Type = type,
            Overview = $"Sinopse de {name}",
            AgeRating = ageRating,
            AgeRatingRank = AgeRatings.Rank(ageRating),
            VoteAverage = vote,
            VoteCount = 1000,
            Popularity = popularity,
            Year = year,
            ReleaseDate = new DateOnly(year, 1, 1),
            PosterUrl = $"https://img/{name}.jpg",
            SearchText = TextNormalizer.SearchText(name, original)
        };

    public static async Task<(ApplicationUser User, Profile Adult, Profile Kids)> UsersAsync(ApplicationDbContext db)
    {
        var user = new ApplicationUser { Id = "user-1", UserName = "u@test.com", Email = "u@test.com", Name = "Usuário" };
        var adult = new Profile { UserId = user.Id, Name = "Adulto", MaturityLimit = "18" };
        var kids = new Profile { UserId = user.Id, Name = "Kids", IsKids = true, MaturityLimit = "10" };
        db.Users.Add(user);
        db.Profiles.AddRange(adult, kids);
        await db.SaveChangesAsync();
        return (user, adult, kids);
    }

    public static async Task<List<Title>> CatalogAsync(ApplicationDbContext db)
    {
        Action = new Genre { Name = "Ação", Slug = "acao", TmdbId = 28 };
        Drama = new Genre { Name = "Drama", Slug = "drama", TmdbId = 18 };
        Animation = new Genre { Name = "Animação", Slug = "animacao", TmdbId = 16 };
        db.Genres.AddRange(Action, Drama, Animation);

        var titles = new List<Title>
        {
            NewTitle("Duna: Parte Dois", ageRating: "14", vote: 8.2, popularity: 100, year: 2024, original: "Dune: Part Two"),
            NewTitle("Coração Valente", ageRating: "16", vote: 8.0, popularity: 40, year: 1995, original: "Braveheart"),
            NewTitle("Divertida Mente", ageRating: "L", vote: 7.9, popularity: 80, year: 2015, original: "Inside Out"),
            NewTitle("Breaking Bad", TitleType.Series, ageRating: "16", vote: 8.9, popularity: 90, year: 2008),
            NewTitle("Bluey", TitleType.Series, ageRating: "L", vote: 8.6, popularity: 30, year: 2018),
            NewTitle("Sem Classificação", ageRating: null, vote: 5, popularity: 5, year: 2010)
        };
        db.Titles.AddRange(titles);
        await db.SaveChangesAsync();

        db.TitleGenres.AddRange(
            new TitleGenre { TitleId = titles[0].Id, GenreId = Action.Id },
            new TitleGenre { TitleId = titles[1].Id, GenreId = Action.Id },
            new TitleGenre { TitleId = titles[1].Id, GenreId = Drama.Id },
            new TitleGenre { TitleId = titles[2].Id, GenreId = Animation.Id },
            new TitleGenre { TitleId = titles[3].Id, GenreId = Drama.Id },
            new TitleGenre { TitleId = titles[4].Id, GenreId = Animation.Id });
        await db.SaveChangesAsync();
        return titles;
    }

    /// <summary>Série com 2 temporadas de 2 episódios.</summary>
    public static async Task<(Title Series, List<Episode> Episodes)> SeriesWithEpisodesAsync(ApplicationDbContext db)
    {
        var series = NewTitle("Dark", TitleType.Series, "16", 8.4, 50, 2017);
        series.RuntimeMinutes = 50;
        db.Titles.Add(series);
        await db.SaveChangesAsync();

        var episodes = new List<Episode>();
        for (var s = 1; s <= 2; s++)
        {
            var season = new Season { TitleId = series.Id, SeasonNumber = s, Name = $"Temporada {s}", EpisodeCount = 2 };
            for (var e = 1; e <= 2; e++)
            {
                var ep = new Episode { EpisodeNumber = e, Name = $"S{s}E{e}", RuntimeMinutes = 50 };
                season.Episodes.Add(ep);
                episodes.Add(ep);
            }
            db.Seasons.Add(season);
        }
        await db.SaveChangesAsync();
        return (series, episodes);
    }
}
