using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieHub.Data;
using MovieHub.Models;
using MovieHub.Services.Tmdb;

namespace MovieHub.Tests.Services;

/// <summary>Importação ponta a ponta contra respostas simuladas no formato real do TMDB.</summary>
public class TmdbImporterTests
{
    private sealed class FakeTmdbHandler : HttpMessageHandler
    {
        public readonly List<string> Requests = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.Replace("/3/", "");
            var query = request.RequestUri.Query;
            Requests.Add(path + query);
            var json = Route(path, query);
            return Task.FromResult(json == null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }

        private static string? Route(string path, string query) => path switch
        {
            "genre/movie/list" => """{"genres":[{"id":28,"name":"Ação"},{"id":878,"name":"Ficção científica"},{"id":12,"name":"Aventura"},{"id":18,"name":"Drama"}]}""",
            "genre/tv/list" => """{"genres":[{"id":10765,"name":"Sci-Fi & Fantasy"},{"id":18,"name":"Drama"},{"id":10767,"name":"Talk"}]}""",
            "movie/popular" when query.Contains("page=1") => """{"page":1,"total_pages":1,"results":[{"id":693134,"adult":false,"popularity":500.1,"genre_ids":[878,12],"poster_path":"/p.jpg"}]}""",
            "tv/popular" when query.Contains("page=1") => """{"page":1,"total_pages":1,"results":[{"id":70523,"adult":false,"popularity":90,"genre_ids":[18,10765],"poster_path":"/d.jpg"},{"id":999,"adult":false,"popularity":80,"genre_ids":[10767],"poster_path":"/talk.jpg"}]}""",
            "movie/693134" when query.Contains("pt-BR") => """
                {"id":693134,"adult":false,"title":"Duna: Parte Dois","original_title":"Dune: Part Two","tagline":"Longa vida aos lutadores.",
                 "overview":"Paul Atreides se une a Chani e aos Fremen.","release_date":"2024-02-27","runtime":166,
                 "poster_path":"/p.jpg","backdrop_path":"/b.jpg","vote_average":8.15,"vote_count":5000,"popularity":500.1,
                 "original_language":"en","status":"Released",
                 "genres":[{"id":878,"name":"Ficção científica"},{"id":12,"name":"Aventura"}],
                 "belongs_to_collection":{"id":726871,"name":"Duna - Coleção","poster_path":"/c.jpg","backdrop_path":"/cb.jpg"},
                 "credits":{"cast":[{"id":1190668,"name":"Timothée Chalamet","character":"Paul Atreides","order":0,"profile_path":"/t.jpg","known_for_department":"Acting","popularity":50},
                                    {"id":505710,"name":"Zendaya","character":"Chani","order":1,"profile_path":"/z.jpg","known_for_department":"Acting","popularity":60}],
                            "crew":[{"id":137427,"name":"Denis Villeneuve","job":"Director","department":"Directing","profile_path":"/dv.jpg"},
                                    {"id":137427,"name":"Denis Villeneuve","job":"Screenplay","department":"Writing","profile_path":"/dv.jpg"},
                                    {"id":1,"name":"Fulano Câmera","job":"Director of Photography","department":"Camera"}]},
                 "videos":{"results":[{"key":"teaserEN","site":"YouTube","type":"Teaser","iso_639_1":"en","official":true},
                                      {"key":"trailerPT","site":"YouTube","type":"Trailer","iso_639_1":"pt","official":true},
                                      {"key":"trailerEN","site":"YouTube","type":"Trailer","iso_639_1":"en","official":true}]},
                 "images":{"logos":[{"file_path":"/logo-en.png","iso_639_1":"en","vote_average":5},{"file_path":"/logo-pt.png","iso_639_1":"pt","vote_average":3}],"backdrops":[]},
                 "release_dates":{"results":[{"iso_3166_1":"US","release_dates":[{"certification":"PG-13","type":3}]},
                                             {"iso_3166_1":"BR","release_dates":[{"certification":"","type":1},{"certification":"14","type":3}]}]},
                 "keywords":{"keywords":[{"id":818,"name":"based on novel or book"},{"id":4565,"name":"dystopia"}]},
                 "watch/providers":{"results":{"BR":{"flatrate":[{"provider_id":1899,"provider_name":"Max","logo_path":"/max.jpg","display_priority":3}],
                                                    "rent":[{"provider_id":2,"provider_name":"Apple TV","logo_path":"/apple.jpg","display_priority":5}]},
                                               "US":{"flatrate":[{"provider_id":9,"provider_name":"Prime","logo_path":"/x.jpg","display_priority":1}]}}}}
                """,
            "collection/726871" => """{"id":726871,"name":"Duna - Coleção","overview":"A saga de Arrakis.","poster_path":"/c.jpg","backdrop_path":"/cb.jpg"}""",
            "tv/70523" when query.Contains("pt-BR") => """
                {"id":70523,"adult":false,"name":"Dark","original_name":"Dark","tagline":"","overview":"",
                 "first_air_date":"2017-12-01","episode_run_time":[],"number_of_seasons":1,"number_of_episodes":2,
                 "poster_path":"/d.jpg","backdrop_path":"/db.jpg","vote_average":8.4,"vote_count":6000,"popularity":90,
                 "original_language":"de","status":"Ended",
                 "genres":[{"id":18,"name":"Drama"},{"id":10765,"name":"Sci-Fi & Fantasy"}],
                 "created_by":[{"id":77,"name":"Baran bo Odar","profile_path":"/bo.jpg"}],
                 "seasons":[{"id":1,"season_number":0,"name":"Especiais","episode_count":3},
                            {"id":2,"season_number":1,"name":"Temporada 1","overview":"","poster_path":"/s1.jpg","air_date":"2017-12-01","episode_count":2}],
                 "credits":{"cast":[{"id":88,"name":"Louis Hofmann","character":"Jonas Kahnwald","order":0}],"crew":[]},
                 "videos":{"results":[]},
                 "images":{"logos":[],"backdrops":[]},
                 "content_ratings":{"results":[{"iso_3166_1":"US","rating":"TV-MA"}]},
                 "keywords":{"results":[{"id":4565,"name":"dystopia"},{"id":9882,"name":"time travel"}]},
                 "watch/providers":{"results":{"BR":{"flatrate":[{"provider_id":8,"provider_name":"Netflix","logo_path":"/nf.jpg","display_priority":0}]}}}}
                """,
            "tv/70523" when query.Contains("en-US") => """{"id":70523,"overview":"A missing child sets four families on a frantic hunt."}""",
            "tv/70523/season/1" => """
                {"season_number":1,"name":"Temporada 1","episodes":[
                  {"episode_number":1,"name":"Segredos","overview":"Jonas volta à escola.","still_path":"/e1.jpg","runtime":51,"air_date":"2017-12-01"},
                  {"episode_number":2,"name":"Mentiras","overview":"","still_path":null,"runtime":45,"air_date":"2017-12-01"}]}
                """,
            _ => """{"page":1,"total_pages":1,"results":[]}"""
        };
    }

    private static (ServiceProvider Provider, FakeTmdbHandler Handler) Build()
    {
        var handler = new FakeTmdbHandler();
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.Configure<TmdbOptions>(o =>
        {
            o.ReadAccessToken = "eyJ-fake-token";
            o.MaxConcurrency = 2;
            o.RequestsPerSecond = 1000;
        });
        services.AddHttpClient(TmdbClient.HttpClientName, c => c.BaseAddress = new Uri("https://api.themoviedb.org/3/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddSingleton<TmdbClient>();
        services.AddSingleton<ImportStatusTracker>();
        services.AddSingleton<ITmdbImporter, TmdbImporter>();
        return (services.BuildServiceProvider(), handler);
    }

    [Fact]
    public async Task ImportCatalog_MapsEverythingAndIsIdempotent()
    {
        var (sp, handler) = Build();
        var importer = sp.GetRequiredService<ITmdbImporter>();
        var job = new ImportJob(true, true, 1, 2, "test");

        await importer.ImportCatalogAsync(job, default);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Talk show é excluído; filme + série importados
            db.Titles.Should().HaveCount(2);

            var movie = await db.Titles
                .Include(t => t.TitleGenres).ThenInclude(g => g.Genre)
                .Include(t => t.TitleProviders).ThenInclude(p => p.Provider)
                .Include(t => t.TitleKeywords)
                .Include(t => t.Collection)
                .SingleAsync(t => t.Type == TitleType.Movie);

            movie.Name.Should().Be("Duna: Parte Dois");
            movie.OriginalName.Should().Be("Dune: Part Two");
            movie.Year.Should().Be(2024);
            movie.RuntimeMinutes.Should().Be(166);
            movie.AgeRating.Should().Be("14", "a certificação BR tem prioridade sobre a dos EUA");
            movie.AgeRatingRank.Should().Be(14);
            movie.TrailerKey.Should().Be("trailerPT", "trailer em português vence teaser e inglês");
            movie.LogoUrl.Should().EndWith("/logo-pt.png");
            movie.PosterUrl.Should().Be("https://image.tmdb.org/t/p/w500/p.jpg");
            movie.BackdropUrl.Should().Be("https://image.tmdb.org/t/p/w1280/b.jpg");
            movie.VoteAverage.Should().Be(8.2);
            movie.SearchText.Should().Contain("duna").And.Contain("dune: part two");
            movie.TitleGenres.Select(g => g.Genre.Name).Should().BeEquivalentTo("Ficção científica", "Aventura");
            movie.TitleProviders.Should().HaveCount(2, "somente provedores do BR (stream + aluguel)");
            movie.TitleProviders.Should().Contain(p => p.Provider.Name == "Max" && p.Kind == ProviderKind.Stream);
            movie.TitleKeywords.Should().HaveCount(2);
            movie.Collection!.Overview.Should().Be("A saga de Arrakis.");
            movie.IsFeatured.Should().BeTrue("primeiro import destaca populares com logo + trailer + backdrop");

            var credits = await db.Credits.Include(c => c.Person).Where(c => c.TitleId == movie.Id).ToListAsync();
            credits.Where(c => c.Role == CreditRole.Cast).OrderBy(c => c.Order).Select(c => c.Character).Should().Equal("Paul Atreides", "Chani");
            credits.Where(c => c.Role == CreditRole.Crew).Select(c => c.Job).Should().BeEquivalentTo("Director", "Screenplay");
            credits.Should().NotContain(c => c.Job == "Director of Photography");
            db.People.Single(p => p.Name == "Timothée Chalamet").SearchName.Should().Be("timothee chalamet");

            var series = await db.Titles.Include(t => t.TitleGenres).ThenInclude(g => g.Genre)
                .SingleAsync(t => t.Type == TitleType.Series);
            series.Overview.Should().StartWith("A missing child", "sinopse vazia em pt-BR cai para en-US");
            series.AgeRating.Should().Be("18", "TV-MA mapeia para 18");
            series.TitleGenres.Select(g => g.Genre.Name).Should().BeEquivalentTo(new[] { "Drama", "Ficção científica" }, "Sci-Fi & Fantasy vira os gêneros de filme equivalentes");
            series.RuntimeMinutes.Should().Be(48, "média dos episódios quando episode_run_time vem vazio");

            var seasons = await db.Seasons.Include(s => s.Episodes).Where(s => s.TitleId == series.Id).ToListAsync();
            seasons.Should().ContainSingle("a temporada 0 (especiais) é ignorada");
            seasons[0].Episodes.Select(e => e.Name).Should().Equal("Segredos", "Mentiras");
            (await db.Credits.CountAsync(c => c.TitleId == series.Id && c.Job == "Creator")).Should().Be(1);

            db.Genres.Should().NotContain(g => g.TmdbId == 10765 || g.TmdbId == 10767, "gêneros de TV mapeados/excluídos");
        }

        var status = sp.GetRequiredService<ImportStatusTracker>().Snapshot();
        status.State.Should().Be("completed");
        status.Imported.Should().Be(2);

        // Reimportar não duplica nada
        await importer.ImportCatalogAsync(job, default);
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Titles.Should().HaveCount(2);
            db.Credits.Should().HaveCount(6);
            db.Episodes.Should().HaveCount(2);
            db.WatchProviders.Should().HaveCount(3);
            db.TitleGenres.Should().HaveCount(4);
        }
    }

    [Fact]
    public async Task Importer_WithoutToken_DegradesGracefully()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.Configure<TmdbOptions>(_ => { });
        services.AddHttpClient(TmdbClient.HttpClientName);
        services.AddSingleton<TmdbClient>();
        services.AddSingleton<ImportStatusTracker>();
        services.AddSingleton<ITmdbImporter, TmdbImporter>();
        var sp = services.BuildServiceProvider();

        var importer = sp.GetRequiredService<ITmdbImporter>();
        importer.IsConfigured.Should().BeFalse();

        await importer.ImportCatalogAsync(new ImportJob(true, true, 10, 10, "test"), default);

        sp.GetRequiredService<ImportStatusTracker>().Snapshot().State.Should().Be("failed");
        (await importer.EnrichPersonAsync(new Person { TmdbId = 1 }, default)).Should().BeFalse();
    }
}
