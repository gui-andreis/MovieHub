using MovieHub.Data.Dtos.Genre;

namespace MovieHub.Services.Interfaces;

public interface IGenreService
{
    Task<IEnumerable<GenreResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GenreResponseDto> CreateAsync(GenreRequestDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}