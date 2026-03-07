using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MovieHub.Data;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Pagination;
using MovieHub.Queries.Movies;
using MovieHub.Services.Interfaces;


namespace MovieHub.Services.Implementations;

public class MovieService : IMovieService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IImageService _imageService;
    private readonly IMemoryCache _cache;

    private static string MovieCacheKey(int id) => $"movie:{id}";

    public MovieService(ApplicationDbContext context, IMapper mapper, IImageService imageService, IMemoryCache cache)
    {
        _context = context;
        _mapper = mapper;
        _imageService = imageService;
        _cache = cache;
    }

    public async Task<MovieResponseDto> CreateAsync(CreateMovieDto dto, CancellationToken cancellationToken = default)
    {
        var movie = _mapper.Map<Movie>(dto);
        movie.ImagePath = await _imageService.SaveImageAsync(dto.Image);

        if (dto.GenreIds.Any())
        {
            var genres = await _context.Genres
                .Where(g => dto.GenreIds.Contains(g.Id))
                .ToListAsync();
            movie.MovieGenres = genres
                .Select(g => new MovieGenre { Genre = g })
                .ToList();
        }

        _context.Movies.Add(movie);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(movie)
            .Collection(m => m.MovieGenres)
            .Query()
            .Include(mg => mg.Genre)
            .LoadAsync();

        var responseDto = _mapper.Map<MovieResponseDto>(movie);
        _cache.Set(MovieCacheKey(movie.Id), responseDto, TimeSpan.FromMinutes(2));
        return responseDto;
    }


    public async Task<MovieResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(MovieCacheKey(id), out MovieResponseDto? cached) && cached != null)
            return cached;

        var movie = await _context.Movies
            .Include(m => m.Reviews)
            .Include(m => m.MovieGenres)
                .ThenInclude(mg => mg.Genre)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Filme com id {id} não encontrado.");

        var dto = _mapper.Map<MovieResponseDto>(movie);

        _cache.Set(MovieCacheKey(id), dto, TimeSpan.FromMinutes(2));

        return dto;
    }

    public async Task UpdateAsync(int id, UpdateMovieDto dto, CancellationToken cancellationToken = default)
    {
        var movie = await _context.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Filme com id {id} não encontrado.");

        if (dto.Image != null)
        {
            _imageService.DeleteImage(movie.ImagePath);
            movie.ImagePath = await _imageService.SaveImageAsync(dto.Image);
        }

        // Substitui gêneros se foram enviados
        if (dto.GenreIds.Any())
        {
            var genres = await _context.Genres
                .Where(g => dto.GenreIds.Contains(g.Id))
                .ToListAsync();
            movie.MovieGenres = genres
                .Select(g => new MovieGenre { MovieId = id, GenreId = g.Id })
                .ToList();
        }

        _mapper.Map(dto, movie);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(MovieCacheKey(id));
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var movie = await _context.Movies.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Filme com id {id} não encontrado.");

        // Remove a imagem do disco junto com o filme
        _imageService.DeleteImage(movie.ImagePath);
        _context.Movies.Remove(movie);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(MovieCacheKey(id));
    }

    public async Task<PagedResult<MovieResponseDto>> GetAllAsync(MovieQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.Movies
            .Include(m => m.Reviews)
            .Include(m => m.MovieGenres)
                .ThenInclude(mg => mg.Genre)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Title))
            query = query.Where(m => m.Title.ToLower().Contains(parameters.Title.ToLower()));

        if (parameters.ReleaseYear.HasValue)
            query = query.Where(m => m.ReleaseYear == parameters.ReleaseYear.Value);

        if (parameters.GenreIds != null && parameters.GenreIds.Any())
            query = query.Where(m => parameters.GenreIds
                .All(id => m.MovieGenres.Any(mg => mg.GenreId == id)));

        if (parameters.MinRating.HasValue)
            query = query.Where(m => m.Reviews.Any() &&
                m.Reviews.Average(r => r.Rating) >= parameters.MinRating.Value);

        if (parameters.MaxRating.HasValue)
            query = query.Where(m => m.Reviews.Any() &&
                m.Reviews.Average(r => r.Rating) <= parameters.MaxRating.Value);

        query = parameters.OrderBy?.ToLower() switch
        {
            "title" => query.OrderBy(m => m.Title),
            "title_desc" => query.OrderByDescending(m => m.Title),
            "year" => query.OrderBy(m => m.ReleaseYear),
            "year_desc" => query.OrderByDescending(m => m.ReleaseYear),
            _ => query.OrderBy(m => m.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)parameters.PageSize);

        var movies = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MovieResponseDto>
        {
            Data = _mapper.Map<List<MovieResponseDto>>(movies),
            CurrentPage = parameters.PageNumber,
            PageSize = parameters.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}