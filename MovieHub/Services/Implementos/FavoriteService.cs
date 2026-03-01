using MovieHub.Data;
using MovieHub.Data.Dtos.Favorite;
using MovieHub.Models;
using MovieHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MovieHub.Exceptions;

namespace MovieHub.Services.Implementos;

public class FavoriteService : IFavoriteService
{
    private readonly ApplicationDbContext _context;

    public FavoriteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(int movieId, string userId)
    {
        var movieExists = await _context.Movies.AnyAsync(m => m.Id == movieId);
        if (!movieExists)
            throw new NotFoundException("Filme não encontrado.");

        var alreadyFavorited = await _context.Favorites
            .AnyAsync(f => f.MovieId == movieId && f.UserId == userId);
        if (alreadyFavorited)
            throw new BadRequestException("Filme já está nos favoritos.");

        var favorite = new Favorite
        {
            MovieId = movieId,
            UserId = userId
        };

        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RemoveAsync(int movieId, string userId)
    {
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.MovieId == movieId && f.UserId == userId);

        if (favorite == null)
            return false;

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<FavoriteReponseDto>> GetMyFavoritesAsync(string userId)
    {
        return await _context.Favorites
            .Include(f => f.Movie)
            .Where(f => f.UserId == userId)
            .Select(f => new FavoriteReponseDto
            {
                MovieId = f.MovieId,
                MovieTitle = f.Movie.Title,
                ReleaseYear = f.Movie.ReleaseYear
            })
            .ToListAsync();
    }
}