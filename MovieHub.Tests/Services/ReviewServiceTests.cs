using FluentAssertions;
using MovieHub.Dtos;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Catalog;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class ReviewServiceTests
{
    [Fact]
    public async Task Create_SecondReviewFromSameProfile_Conflicts_ButOtherProfileCanReview()
    {
        var db = TestDbContextFactory.Create();
        var titles = await Seed.CatalogAsync(db);
        var (_, adult, kids) = await Seed.UsersAsync(db);
        var current = new FakeCurrentProfile { UserId = adult.UserId, Profile = adult };
        var service = new ReviewService(db, current);

        await service.CreateAsync(titles[0].Id, new ReviewRequest { Rating = 9, Comment = "Incrível" }, default);

        await service.Invoking(s => s.CreateAsync(titles[0].Id, new ReviewRequest { Rating = 3 }, default))
            .Should().ThrowAsync<ConflictException>();

        current.Profile = kids;
        var other = await service.CreateAsync(titles[0].Id, new ReviewRequest { Rating = 7 }, default);
        other.Author.ProfileName.Should().Be("Kids");
    }

    [Fact]
    public async Task Summary_ReturnsAverageAndFullDistribution()
    {
        var db = TestDbContextFactory.Create();
        var titles = await Seed.CatalogAsync(db);
        var (_, adult, kids) = await Seed.UsersAsync(db);
        db.Reviews.AddRange(
            new Review { ProfileId = adult.Id, TitleId = titles[0].Id, Rating = 10 },
            new Review { ProfileId = kids.Id, TitleId = titles[0].Id, Rating = 7 });
        await db.SaveChangesAsync();

        var summary = await new ReviewService(db, new FakeCurrentProfile()).GetSummaryAsync(titles[0].Id, default);

        summary.Count.Should().Be(2);
        summary.Average.Should().Be(8.5);
        summary.Distribution.Should().HaveCount(10);
        summary.Distribution["10"].Should().Be(1);
        summary.Distribution["1"].Should().Be(0);
    }

    [Fact]
    public async Task Update_ByAnotherUser_IsForbidden_ButAdminCanDelete()
    {
        var db = TestDbContextFactory.Create();
        var titles = await Seed.CatalogAsync(db);
        var (_, adult, _) = await Seed.UsersAsync(db);
        var review = new Review { ProfileId = adult.Id, TitleId = titles[0].Id, Rating = 8 };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        var stranger = new FakeCurrentProfile { UserId = "someone-else" };
        var service = new ReviewService(db, stranger);

        await service.Invoking(s => s.UpdateAsync(review.Id, new ReviewRequest { Rating = 1 }, default))
            .Should().ThrowAsync<ForbiddenException>();

        stranger.IsAdmin = true;
        await service.DeleteAsync(review.Id, default);
        db.Reviews.Should().BeEmpty();
    }

    [Fact]
    public async Task List_MarksMyReviews_AndSortsByRating()
    {
        var db = TestDbContextFactory.Create();
        var titles = await Seed.CatalogAsync(db);
        var (_, adult, kids) = await Seed.UsersAsync(db);
        db.Reviews.AddRange(
            new Review { ProfileId = adult.Id, TitleId = titles[0].Id, Rating = 4 },
            new Review { ProfileId = kids.Id, TitleId = titles[0].Id, Rating = 9 });
        await db.SaveChangesAsync();

        var service = new ReviewService(db, new FakeCurrentProfile { UserId = adult.UserId, Profile = adult });
        var page = await service.GetForTitleAsync(titles[0].Id, new ReviewQueryParameters { Sort = "rating_desc" }, default);

        page.Items.Select(r => r.Rating).Should().Equal(9, 4);
        page.Items.Single(r => r.Rating == 4).IsMine.Should().BeTrue();
        page.Items.Single(r => r.Rating == 9).IsMine.Should().BeFalse();
    }
}
