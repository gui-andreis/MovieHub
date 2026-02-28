namespace MovieHub.Services.Interfaces;

using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Models;

public class MovieService : IMovieService
{
    private readonly ApplicationDbContext _context;

    public MovieService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MovieResponseDto> CreateAsync(CreateMovieDto dto)
    {
        var movie = new Movie
        {
            Title = dto.Title,
            Description = dto.Description,
            ReleaseYear = dto.ReleaseYear
        };

        _context.Movies.Add(movie);
        await _context.SaveChangesAsync();

        return new MovieResponseDto
        {
            Id = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            ReleaseYear = movie.ReleaseYear
        };
    }

    public async Task<IEnumerable<MovieResponseDto>> GetAllAsync()
    {
        return await _context.Movies
            .Select(m => new MovieResponseDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                ReleaseYear = m.ReleaseYear
            })
            .ToListAsync();
    }

    public async Task<MovieResponseDto?> GetByIdAsync(int id)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie == null)
            return null;

        return new MovieResponseDto
        {
            Id = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            ReleaseYear = movie.ReleaseYear
        };
    }

    public async Task<bool> UpdateAsync(int id, UpdateMovieDto dto)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie == null)
            return false;

        movie.Title = dto.Title;
        movie.Description = dto.Description;
        movie.ReleaseYear = dto.ReleaseYear;

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
}