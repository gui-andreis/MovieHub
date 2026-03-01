using MovieHub.Data.Dtos.Review;

namespace MovieHub.Services.Interfaces;

public interface IReviewService
{
    // Cria uma nova review associada ao usuário autenticado
    Task CreateAsync(CreateReviewDto dto, string userId);

    // Retorna todas as reviews de um filme específico
    Task<IEnumerable<ReviewResponseDto>> GetAllByMovieAsync(int movieId);

    // Retorna todas as reviews do usuário autenticado
    Task<IEnumerable<ReviewResponseDto>> GetMyReviewsAsync(string userId);

    // Atualiza uma review, garantindo que pertence ao usuário
    Task<bool> UpdateAsync(int id, UpdateReviewDto dto, string userId);

    // Remove uma review (admin pode remover qualquer uma)
    Task<bool> DeleteAsync(int id, string userId, bool isAdmin);
}