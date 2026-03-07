using MovieHub.Data.Dtos.Favorite;

namespace MovieHub.Services.Interfaces;

public interface IFavoriteService
{
    Task AddAsync(int movieId, string userId, CancellationToken cancellationToken = default);
    Task RemoveAsync(int movieId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<FavoriteResponseDto>> GetMyFavoritesAsync(string userId, CancellationToken cancellationToken = default);
}