using AutoMapper;
using FluentAssertions;
using MovieHub.Data;
using MovieHub.Data.Dtos.Review;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Profiles;
using MovieHub.Services.Implementations;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class ReviewServiceTests
{
    private readonly IMapper _mapper;

    public ReviewServiceTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ReviewProfile>();
        });
        _mapper = config.CreateMapper();
    }

    private ReviewService CreateService(ApplicationDbContext context)
        => new ReviewService(context, _mapper);

    // helper — cria um filme no banco e retorna o Id
    private async Task<int> SeedMovieAsync(ApplicationDbContext context)
    {
        var movie = new Movie { Title = "Inception", Description = "Sonhos", ReleaseYear = 2010 };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie.Id;
    }

    // -------------------------
    // CreateAsync
    // -------------------------

    [Fact]
    public async Task CreateAsync_ComDadosValidos_CriaReview()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var service = CreateService(context);

        var dto = new CreateReviewDto { MovieId = movieId, Rating = 8, Comment = "Ótimo!" };

        // Act
        await service.CreateAsync(dto, "user-123");

        // Assert
        context.Reviews.Count().Should().Be(1);
        context.Reviews.First().UserId.Should().Be("user-123");
    }

    [Fact]
    public async Task CreateAsync_FilmeNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var dto = new CreateReviewDto { MovieId = 999, Rating = 5, Comment = "X" };

        // Act
        var act = async () => await service.CreateAsync(dto, "user-123");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_ReviewDuplicada_LancaConflictException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var service = CreateService(context);

        var dto = new CreateReviewDto { MovieId = movieId, Rating = 7, Comment = "Legal" };

        // primeira review
        await service.CreateAsync(dto, "user-123");

        // Act — tenta criar segunda review pro mesmo filme
        var act = async () => await service.CreateAsync(dto, "user-123");

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*já avaliou*");
    }

    // -------------------------
    // UpdateAsync
    // -------------------------

    [Fact]
    public async Task UpdateAsync_DonoDaReview_Atualiza()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var review = new Review { MovieId = movieId, UserId = "user-123", Rating = 5, Comment = "Ok", CreatedAt = DateTime.UtcNow };
        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = new UpdateReviewDto { Rating = 9, Comment = "Mudei de ideia!" };

        // Act
        await service.UpdateAsync(review.Id, dto, "user-123");

        // Assert
        var updated = await context.Reviews.FindAsync(review.Id);
        updated!.Rating.Should().Be(9);
        updated.Comment.Should().Be("Mudei de ideia!");
    }

    [Fact]
    public async Task UpdateAsync_UsuarioDiferente_LancaForbiddenException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var review = new Review { MovieId = movieId, UserId = "user-123", Rating = 5, Comment = "Ok", CreatedAt = DateTime.UtcNow };
        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = new UpdateReviewDto { Rating = 1, Comment = "Hacker!" };

        // Act
        var act = async () => await service.UpdateAsync(review.Id, dto, "outro-user");

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_ReviewNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        var dto = new UpdateReviewDto { Rating = 5, Comment = "X" };

        // Act
        var act = async () => await service.UpdateAsync(999, dto, "user-123");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // -------------------------
    // DeleteAsync
    // -------------------------

    [Fact]
    public async Task DeleteAsync_DonoDaReview_Remove()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var review = new Review { MovieId = movieId, UserId = "user-123", Rating = 5, Comment = "Ok", CreatedAt = DateTime.UtcNow };
        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        await service.DeleteAsync(review.Id, "user-123", isAdmin: false);

        // Assert
        context.Reviews.Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_AdminRemoveReviewDeOutro()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var review = new Review { MovieId = movieId, UserId = "user-123", Rating = 5, Comment = "Ok", CreatedAt = DateTime.UtcNow };
        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act — admin diferente do dono
        await service.DeleteAsync(review.Id, "admin-456", isAdmin: true);

        // Assert
        context.Reviews.Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_UsuarioDiferente_LancaForbiddenException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var review = new Review { MovieId = movieId, UserId = "user-123", Rating = 5, Comment = "Ok", CreatedAt = DateTime.UtcNow };
        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var act = async () => await service.DeleteAsync(review.Id, "outro-user", isAdmin: false);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}