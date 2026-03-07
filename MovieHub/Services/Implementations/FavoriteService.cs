using MovieHub.Data;
using MovieHub.Data.Dtos.Favorite;
using MovieHub.Models;
using MovieHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MovieHub.Exceptions;

namespace MovieHub.Services.Implementations;

public class FavoriteService : IFavoriteService
{
    private readonly ApplicationDbContext _context;

    // Injeta DbContext para acesso à persistência
    public FavoriteService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Adiciona um filme à lista de favoritos do usuário
    public async Task AddAsync(int movieId, string userId, CancellationToken cancellationToken = default)
    {
        var movieExists = await _context.Movies.AnyAsync(m => m.Id == movieId, cancellationToken);
        if (!movieExists)
            throw new NotFoundException("Filme não encontrado.");

        // Impede que o mesmo filme seja favoritado mais de uma vez pelo mesmo usuário
        var alreadyFavorited = await _context.Favorites
            .AnyAsync(f => f.MovieId == movieId && f.UserId == userId, cancellationToken);
        if (alreadyFavorited)
            throw new ConflictException("Filme já está nos favoritos.");

        var favorite = new Favorite
        {
            MovieId = movieId,
            UserId = userId
        };

        _context.Favorites.Add(favorite);

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Remove um filme da lista de favoritos do usuário
    public async Task RemoveAsync(int movieId, string userId, CancellationToken cancellationToken = default)
    {
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.MovieId == movieId && f.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Favorito não encontrado.");

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Retorna lista de filmes favoritados pelo usuário
    // Usa projection direta para DTO (evita carregar entidade inteira desnecessariamente)
    public async Task<IEnumerable<FavoriteResponseDto>> GetMyFavoritesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Favorites
            .Include(f => f.Movie)
            .Where(f => f.UserId == userId)
            .Select(f => new FavoriteResponseDto
            {
                MovieId = f.MovieId,
                MovieTitle = f.Movie.Title,
                ReleaseYear = f.Movie.ReleaseYear
            })
            .ToListAsync(cancellationToken);
    }
}