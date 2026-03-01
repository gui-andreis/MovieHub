using MovieHub.Data.Dtos.Review;

namespace MovieHub.Services.Interfaces;

public interface IReviewService
{
    Task CreateAsync(CreateReviewDto dto, string userId);
    Task<IEnumerable<ReviewResponseDto>> GetAllByMovieAsync(int movieId);
    Task<IEnumerable<ReviewResponseDto>> GetMyReviewsAsync(string userId);
    Task<bool> UpdateAsync(int id, UpdateReviewDto dto, string userId);

    Task<bool> DeleteAsync(int id, string userId, bool isAdmin);
}