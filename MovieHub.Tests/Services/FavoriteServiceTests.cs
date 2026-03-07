using FluentAssertions;
using MovieHub.Data;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Services.Implementations;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class FavoriteServiceTests
{
    private FavoriteService CreateService(ApplicationDbContext context)
        => new FavoriteService(context);

    private async Task<int> SeedMovieAsync(ApplicationDbContext context)
    {
        var movie = new Movie { Title = "Inception", Description = "Sonhos", ReleaseYear = 2010 };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie.Id;
    }

    // -------------------------
    // AddAsync
    // -------------------------

    [Fact]
    public async Task AddAsync_ComDadosValidos_AdicionaFavorito()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var service = CreateService(context);

        // Act
        await service.AddAsync(movieId, "user-123");

        // Assert
        context.Favorites.Count().Should().Be(1);
        context.Favorites.First().UserId.Should().Be("user-123");
        context.Favorites.First().MovieId.Should().Be(movieId);
    }

    [Fact]
    public async Task AddAsync_FilmeNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Act
        var act = async () => await service.AddAsync(999, "user-123");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddAsync_FavoritoDuplicado_LancaConflictException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var service = CreateService(context);

        await service.AddAsync(movieId, "user-123");

        // Act — tenta favoritar de novo
        var act = async () => await service.AddAsync(movieId, "user-123");

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*favoritos*");
    }

    [Fact]
    public async Task AddAsync_UsuariosDiferentesPodemFavoritarMesmoFilme()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var service = CreateService(context);

        // Act
        await service.AddAsync(movieId, "user-123");
        await service.AddAsync(movieId, "user-456");

        // Assert
        context.Favorites.Count().Should().Be(2);
    }

    // -------------------------
    // RemoveAsync
    // -------------------------

    [Fact]
    public async Task RemoveAsync_FavoritoExiste_Remove()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId = await SeedMovieAsync(context);
        var service = CreateService(context);

        await service.AddAsync(movieId, "user-123");

        // Act
        await service.RemoveAsync(movieId, "user-123");

        // Assert
        context.Favorites.Count().Should().Be(0);
    }

    [Fact]
    public async Task RemoveAsync_FavoritoNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Act
        var act = async () => await service.RemoveAsync(999, "user-123");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // -------------------------
    // GetMyFavoritesAsync
    // -------------------------

    [Fact]
    public async Task GetMyFavoritesAsync_RetornaApenasDoUsuario()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movieId1 = await SeedMovieAsync(context);
        var movieId2 = await SeedMovieAsync(context);
        var service = CreateService(context);

        await service.AddAsync(movieId1, "user-123");
        await service.AddAsync(movieId2, "user-123");
        await service.AddAsync(movieId1, "user-456"); // outro usuário

        // Act
        var result = await service.GetMyFavoritesAsync("user-123");

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(f => f.MovieId == movieId1 || f.MovieId == movieId2);
    }

    [Fact]
    public async Task GetMyFavoritesAsync_SemFavoritos_RetornaListaVazia()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Act
        var result = await service.GetMyFavoritesAsync("user-123");

        // Assert
        result.Should().BeEmpty();
    }
}