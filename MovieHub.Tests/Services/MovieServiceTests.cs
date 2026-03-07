using AutoMapper;
using FluentAssertions;
using Moq;
using MovieHub.Data;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Profiles;
using MovieHub.Services.Implementations;
using MovieHub.Services.Interfaces;
using MovieHub.Tests.Helpers;

namespace MovieHub.Tests.Services;

public class MovieServiceTests
{
    private readonly IMapper _mapper;
    private readonly Mock<IImageService> _imageServiceMock;

    public MovieServiceTests()
    {
        // Configura AutoMapper com os profiles reais — testa o mapeamento também
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MovieProfile>();
        });
        _mapper = config.CreateMapper();
        _imageServiceMock = new Mock<IImageService>();
    }

    // helper pra criar o service com banco limpo a cada teste
    private MovieService CreateService(ApplicationDbContext context)
        => new MovieService(context, _mapper, _imageServiceMock.Object);

    // -------------------------
    // GetByIdAsync
    // -------------------------

    [Fact]
    public async Task GetByIdAsync_QuandoFilmeExiste_RetornaFilme()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movie = new Movie { Title = "Inception", Description = "Sonhos", ReleaseYear = 2010 };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.GetByIdAsync(movie.Id);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Inception");
        result.ReleaseYear.Should().Be(2010);
    }

    [Fact]
    public async Task GetByIdAsync_QuandoFilmeNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Act
        var act = async () => await service.GetByIdAsync(999);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*999*"); // verifica que a mensagem menciona o id
    }

    // -------------------------
    // CreateAsync
    // -------------------------

    [Fact]
    public async Task CreateAsync_ComDadosValidos_CriaERetornaFilme()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        _imageServiceMock
            .Setup(x => x.SaveImageAsync(null))
            .ReturnsAsync((string?)null);

        var dto = new CreateMovieDto
        {
            Title = "Interstellar",
            Description = "Espaço",
            ReleaseYear = 2014,
            GenreIds = new List<int>()
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Interstellar");
        context.Movies.Count().Should().Be(1);
    }

    // -------------------------
    // DeleteAsync
    // -------------------------

    [Fact]
    public async Task DeleteAsync_QuandoFilmeExiste_RemoveDobanco()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movie = new Movie { Title = "Avatar", Description = "Azul", ReleaseYear = 2009 };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        _imageServiceMock.Setup(x => x.DeleteImage(It.IsAny<string>()));

        // Act
        await service.DeleteAsync(movie.Id);

        // Assert
        context.Movies.Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_QuandoFilmeNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Act
        var act = async () => await service.DeleteAsync(999);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // -------------------------
    // UpdateAsync
    // -------------------------

    [Fact]
    public async Task UpdateAsync_QuandoFilmeExiste_AtualizaDados()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var movie = new Movie { Title = "Antigo", Description = "Desc", ReleaseYear = 2000 };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var dto = new UpdateMovieDto
        {
            Title = "Novo Título",
            Description = "Nova Desc",
            ReleaseYear = 2024,
            GenreIds = new List<int>()
        };

        // Act
        await service.UpdateAsync(movie.Id, dto);

        // Assert
        var updated = await context.Movies.FindAsync(movie.Id);
        updated!.Title.Should().Be("Novo Título");
        updated.ReleaseYear.Should().Be(2024);
    }

    [Fact]
    public async Task UpdateAsync_QuandoFilmeNaoExiste_LancaNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var dto = new UpdateMovieDto
        {
            Title = "X",
            Description = "X",
            ReleaseYear = 2024,
            GenreIds = new List<int>()
        };

        // Act
        var act = async () => await service.UpdateAsync(999, dto);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}