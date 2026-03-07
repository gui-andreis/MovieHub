using AutoMapper;
using FluentAssertions;
using MovieHub.Data;
using MovieHub.Data.Dtos.Genre;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Profiles;
using MovieHub.Services.Implementations;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class GenreServiceTests
{
    private readonly IMapper _mapper;

    public GenreServiceTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<GenreProfile>();
        });
        _mapper = config.CreateMapper();
    }

    private GenreService CreateService(ApplicationDbContext context)
        => new GenreService(context, _mapper);

    // -------------------------
    // GetAllAsync
    // -------------------------

    [Fact]
    public async Task GetAllAsync_SemGeneros_RetornaListaVazia()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ComGeneros_RetornaOrdenadosPorNome()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        context.Genres.AddRange(
            new Genre { Name = "Terror" },
            new Genre { Name = "Ação" },
            new Genre { Name = "Drama" }
        );
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(g => g.Name).Should().BeInAscendingOrder();
    }

    // -------------------------
    // CreateAsync
    // -------------------------

    [Fact]
    public async Task CreateAsync_ComDadosValidos_CriaGenero()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);
        var dto = new GenreRequestDto { Name = "Ficção Científica" };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Ficção Científica");
        context.Genres.Count().Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_NomeDuplicado_LancaConflictException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        context.Genres.Add(new Genre { Name = "Ação" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var dto = new GenreRequestDto { Name = "Ação" };

        // Act
        var act = async () => await service.CreateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Ação*");
    }

    // -------------------------
    // DeleteAsync
    // -------------------------

    [Fact]
    public async Task DeleteAsync_GeneroExiste_Remove()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var genre = new Genre { Name = "Terror" };
        context.Genres.Add(genre);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        await service.DeleteAsync(genre.Id);

        // Assert
        context.Genres.Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_GeneroNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Act
        var act = async () => await service.DeleteAsync(999);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}