using MovieHub.Data.Dtos.Favorite;

namespace MovieHub.Services.Interfaces;

public interface IFavoriteService
{
    // Adiciona um filme aos favoritos do usuário
    Task AddAsync(int movieId, string userId);

    // Remove um filme dos favoritos do usuário
    Task<bool> RemoveAsync(int movieId, string userId);

    // Retorna lista de filmes favoritados pelo usuário
    Task<IEnumerable<FavoriteResponseDto>> GetMyFavoritesAsync(string userId);
}