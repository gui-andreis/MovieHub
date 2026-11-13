using FluentAssertions;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Catalog;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class CatalogServiceTests
{
    private static async Task<(CatalogService Service, FakeCurrentProfile Current, List<Title> Titles, Profile Adult, Profile Kids)> SetupAsync()
    {
        var db = TestDbContextFactory.Create();
        var titles = await Seed.CatalogAsync(db);
        var (_, adult, kids) = await Seed.UsersAsync(db);
        var current = new FakeCurrentProfile();
        return (new CatalogService(db, current), current, titles, adult, kids);
    }

    [Fact]
    public async Task List_WithoutFilters_ReturnsEverythingOrderedByPopularity()
    {
        var (service, _, titles, _, _) = await SetupAsync();

        var result = await service.ListAsync(new TitleQueryParameters(), default);

        result.TotalCount.Should().Be(titles.Count);
        result.Items.First().Name.Should().Be("Duna: Parte Dois");
        result.Items.Select(i => titles.First(t => t.Id == i.Id).Popularity).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task List_FiltersByTypeGenreAndRating()
    {
        var (service, _, _, _, _) = await SetupAsync();

        var series = await service.ListAsync(new TitleQueryParameters { Type = "series" }, default);
        series.Items.Should().OnlyContain(t => t.Type == TitleType.Series).And.HaveCount(2);

        var action = await service.ListAsync(new TitleQueryParameters { GenreIds = Seed.Action.Id.ToString() }, default);
        action.Items.Select(t => t.Name).Should().BeEquivalentTo("Duna: Parte Dois", "Coração Valente");

        var acclaimed = await service.ListAsync(new TitleQueryParameters { MinRating = 8.5 }, default);
        acclaimed.Items.Select(t => t.Name).Should().BeEquivalentTo("Breaking Bad", "Bluey");

        var nineties = await service.ListAsync(new TitleQueryParameters { YearFrom = 1990, YearTo = 1999 }, default);
        nineties.Items.Should().ContainSingle(t => t.Name == "Coração Valente");
    }

    [Fact]
    public async Task List_SearchIsAccentAndCaseInsensitive_AndMatchesOriginalName()
    {
        var (service, _, _, _, _) = await SetupAsync();

        (await service.ListAsync(new TitleQueryParameters { Q = "CORACAO" }, default))
            .Items.Should().ContainSingle(t => t.Name == "Coração Valente");

        (await service.ListAsync(new TitleQueryParameters { Q = "braveheart" }, default))
            .Items.Should().ContainSingle(t => t.Name == "Coração Valente");
    }

    [Fact]
    public async Task List_SortsAndPaginates()
    {
        var (service, _, _, _, _) = await SetupAsync();

        var page = await service.ListAsync(new TitleQueryParameters { Sort = "rating", Page = 1, PageSize = 2 }, default);

        page.TotalCount.Should().Be(6);
        page.TotalPages.Should().Be(3);
        page.Items.Select(i => i.Name).Should().Equal("Breaking Bad", "Bluey");
    }

    [Fact]
    public async Task KidsProfile_OnlySeesTitlesUpTo10_AndUnratedAreHidden()
    {
        var (service, current, _, _, kids) = await SetupAsync();
        current.UserId = kids.UserId;
        current.Profile = kids;

        var result = await service.ListAsync(new TitleQueryParameters { PageSize = 50 }, default);

        result.Items.Select(i => i.Name).Should().BeEquivalentTo("Divertida Mente", "Bluey");
    }

    [Fact]
    public async Task KidsProfile_CannotOpenAdultTitleDetail()
    {
        var (service, current, titles, _, kids) = await SetupAsync();
        current.UserId = kids.UserId;
        current.Profile = kids;

        var breakingBad = titles.First(t => t.Name == "Breaking Bad");
        var act = () => service.GetDetailAsync(breakingBad.Id, default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AdultProfile_SeesEverything()
    {
        var (service, current, titles, adult, _) = await SetupAsync();
        current.UserId = adult.UserId;
        current.Profile = adult;

        var result = await service.ListAsync(new TitleQueryParameters { PageSize = 50 }, default);
        result.TotalCount.Should().Be(titles.Count);
    }

    [Fact]
    public async Task Similar_PrefersSharedGenres_AndExcludesSelf()
    {
        var (service, _, titles, _, _) = await SetupAsync();
        var duna = titles.First(t => t.Name == "Duna: Parte Dois");

        var similar = await service.GetSimilarAsync(duna.Id, 10, default);

        similar.Should().NotContain(t => t.Id == duna.Id);
        similar.First().Name.Should().Be("Coração Valente");
    }

    [Fact]
    public async Task Detail_IncludesMeBlock_WhenProfilePresent()
    {
        var (service, current, titles, adult, _) = await SetupAsync();
        current.UserId = adult.UserId;
        current.Profile = adult;

        var detail = await service.GetDetailAsync(titles[0].Id, default);

        detail.Me.Should().NotBeNull();
        detail.Me!.InMyList.Should().BeFalse();
        detail.Genres.Should().ContainSingle(g => g.Slug == "acao");
    }

    [Fact]
    public async Task Search_ReturnsTitlesAndPeople()
    {
        var db = TestDbContextFactory.Create();
        await Seed.CatalogAsync(db);
        db.People.Add(new Person { TmdbId = 1, Name = "Zendaya", SearchName = "zendaya" });
        await db.SaveChangesAsync();
        var service = new CatalogService(db, new FakeCurrentProfile());

        var result = await service.SearchAsync("zen", default);
        result.People.Should().ContainSingle(p => p.Name == "Zendaya");

        var titles = await service.SearchAsync("duna", default);
        titles.Titles.Should().ContainSingle();
    }
}
