using MovieHub.Data.Dtos.Review;

namespace MovieHub.Services.Interfaces;

public interface IReviewService
{
    Task CreateAsync(CreateReviewDto dto, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ReviewResponseDto>> GetAllByMovieAsync(int movieId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ReviewResponseDto>> GetMyReviewsAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, UpdateReviewDto dto, string userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, string userId, bool isAdmin, CancellationToken cancellationToken = default);
}