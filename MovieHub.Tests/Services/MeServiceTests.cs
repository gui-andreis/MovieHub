using FluentAssertions;
using MovieHub.Data;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Catalog;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class MeServiceTests
{
    private static async Task<(MeService Service, ApplicationDbContext Db, FakeCurrentProfile Current, List<Title> Titles, Profile Adult)> SetupAsync()
    {
        var db = TestDbContextFactory.Create();
        var titles = await Seed.CatalogAsync(db);
        var (_, adult, _) = await Seed.UsersAsync(db);
        var current = new FakeCurrentProfile { UserId = adult.UserId, Profile = adult };
        return (new MeService(db, current), db, current, titles, adult);
    }

    [Fact]
    public async Task MyList_AddIsIdempotent_AndRemoveWorks()
    {
        var (service, db, _, titles, adult) = await SetupAsync();

        await service.AddToListAsync(titles[0].Id, default);
        await service.AddToListAsync(titles[0].Id, default);
        await service.AddToListAsync(titles[1].Id, default);

        db.MyListItems.Count(m => m.ProfileId == adult.Id).Should().Be(2);
        (await service.GetMyListAsync(default)).Should().HaveCount(2);

        var removed = await service.RemoveFromListAsync(titles[0].Id, default);
        removed.InMyList.Should().BeFalse();
        (await service.GetMyListAsync(default)).Should().ContainSingle(t => t.Id == titles[1].Id);
    }

    [Fact]
    public async Task MyList_UnknownTitle_Throws404()
    {
        var (service, _, _, _, _) = await SetupAsync();
        await service.Invoking(s => s.AddToListAsync(9999, default)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task MyList_RequiresProfile()
    {
        var (service, _, current, titles, _) = await SetupAsync();
        current.Profile = null;
        await service.Invoking(s => s.AddToListAsync(titles[0].Id, default)).Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Progress_PartialMovie_AppearsInContinueWatching()
    {
        var (service, _, _, titles, _) = await SetupAsync();

        var result = await service.SaveProgressAsync(new ProgressRequest { TitleId = titles[0].Id, PositionSeconds = 1800, DurationSeconds = 6000 }, default);

        result.Completed.Should().BeFalse();
        result.Percent.Should().Be(30);
        var cw = await service.GetContinueWatchingAsync(default);
        cw.Should().ContainSingle(c => c.Title.Id == titles[0].Id && c.Percent == 30);
    }

    [Fact]
    public async Task Progress_At90Percent_CompletesMovie_AndLeavesContinueWatching()
    {
        var (service, _, _, titles, _) = await SetupAsync();

        await service.SaveProgressAsync(new ProgressRequest { TitleId = titles[0].Id, PositionSeconds = 100, DurationSeconds = 6000 }, default);
        var result = await service.SaveProgressAsync(new ProgressRequest { TitleId = titles[0].Id, PositionSeconds = 5500, DurationSeconds = 6000 }, default);

        result.Completed.Should().BeTrue();
        (await service.GetContinueWatchingAsync(default)).Should().BeEmpty();
        (await service.GetStatsAsync(default)).WatchedCount.Should().Be(1);
    }

    [Fact]
    public async Task Progress_CompletingEpisode_QueuesNextEpisode()
    {
        var (service, db, _, _, _) = await SetupAsync();
        var (series, episodes) = await Seed.SeriesWithEpisodesAsync(db);
        var s1e2 = episodes[1];

        var result = await service.SaveProgressAsync(new ProgressRequest
        {
            TitleId = series.Id, EpisodeId = s1e2.Id, PositionSeconds = 2950, DurationSeconds = 3000
        }, default);

        result.Completed.Should().BeTrue();
        result.NextEpisode.Should().NotBeNull();
        result.NextEpisode!.SeasonNumber.Should().Be(2);
        result.NextEpisode.EpisodeNumber.Should().Be(1);

        var cw = await service.GetContinueWatchingAsync(default);
        cw.Should().ContainSingle();
        cw[0].Episode!.Id.Should().Be(episodes[2].Id);
        cw[0].PositionSeconds.Should().Be(0);
    }

    [Fact]
    public async Task Progress_EpisodeFromOtherTitle_IsRejected()
    {
        var (service, db, _, titles, _) = await SetupAsync();
        var (_, episodes) = await Seed.SeriesWithEpisodesAsync(db);

        await service.Invoking(s => s.SaveProgressAsync(new ProgressRequest
        {
            TitleId = titles[0].Id, EpisodeId = episodes[0].Id, PositionSeconds = 10, DurationSeconds = 100
        }, default)).Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task RemoveProgress_RemovesFromContinueWatching()
    {
        var (service, _, _, titles, _) = await SetupAsync();
        await service.SaveProgressAsync(new ProgressRequest { TitleId = titles[1].Id, PositionSeconds = 60, DurationSeconds = 6000 }, default);

        await service.RemoveProgressAsync(titles[1].Id, default);

        (await service.GetContinueWatchingAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Reactions_UpsertAndRemove_AndCountInStats()
    {
        var (service, _, _, titles, _) = await SetupAsync();

        await service.SetReactionAsync(titles[0].Id, ReactionValue.Like, default);
        var updated = await service.SetReactionAsync(titles[0].Id, ReactionValue.Love, default);
        updated.Value.Should().Be(ReactionValue.Love);

        var stats = await service.GetStatsAsync(default);
        stats.LikesCount.Should().Be(1);
        stats.TopGenres.Should().ContainSingle(g => g.Name == "Ação");

        (await service.RemoveReactionAsync(titles[0].Id, default)).Value.Should().BeNull();
    }
}
