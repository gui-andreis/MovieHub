namespace MovieHub.Services.Interfaces;

using MovieHub.Data.Dtos.Movie;
using MovieHub.Pagination;
using MovieHub.Queries.Movies;
using System.Threading.Tasks;


public interface IMovieService
{
    Task<MovieResponseDto> CreateAsync(CreateMovieDto dto, CancellationToken cancellationToken = default);
    Task<PagedResult<MovieResponseDto>> GetAllAsync(MovieQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<MovieResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, UpdateMovieDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
