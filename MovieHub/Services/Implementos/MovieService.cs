using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Models;
using MovieHub.Queries.Movies;
using MovieHub.Services.Interfaces;
using MovieHub.Pagination;


namespace MovieHub.Services.Implementos;
public class MovieService : IMovieService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public MovieService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<MovieResponseDto> CreateAsync(CreateMovieDto dto)
    {
        var movie = _mapper.Map<Movie>(dto);

        _context.Movies.Add(movie);
        await _context.SaveChangesAsync();

        return _mapper.Map<MovieResponseDto>(movie);
    }


    public async Task<MovieResponseDto?> GetByIdAsync(int id)
    {
        var movie = await _context.Movies
            .Include(m => m.Reviews)  // <-- carrega as reviews
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movie == null)
            return null;
        return _mapper.Map<MovieResponseDto>(movie);
    }

    public async Task<bool> UpdateAsync(int id, UpdateMovieDto dto)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie == null)
            return false;

        _mapper.Map(dto, movie);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie == null)
            return false;

        _context.Movies.Remove(movie);
        await _context.SaveChangesAsync();
        return true;
    }
    public async Task<PagedResult<MovieResponseDto>> GetAllAsync(MovieQueryParameters parameters)
    {
        var query = _context.Movies.AsQueryable();

        var totalCount = await query.CountAsync();

        var movies = await query
            .Include(m => m.Reviews)
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)parameters.PageSize);

        var movieDtos = _mapper.Map<List<MovieResponseDto>>(movies);

        return new PagedResult<MovieResponseDto>
        {
            Data = movieDtos,
            CurrentPage = parameters.PageNumber,
            PageSize = parameters.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}