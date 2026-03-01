using MovieHub.Data.Dtos.Favorite;

namespace MovieHub.Services.Interfaces;

public interface IFavoriteService
{
    Task AddAsync(int movieId, string userId);
    Task<bool> RemoveAsync(int movieId, string userId);
    Task<IEnumerable<FavoriteReponseDto>> GetMyFavoritesAsync(string userId);
}