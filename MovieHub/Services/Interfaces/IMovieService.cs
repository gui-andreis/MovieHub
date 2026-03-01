namespace MovieHub.Services.Interfaces;

using MovieHub.Data.Dtos.Movie;
using MovieHub.Queries.Movies;
using System.Threading.Tasks;
using MovieHub.Pagination;


public interface IMovieService
{
    Task<MovieResponseDto> CreateAsync(CreateMovieDto dto);

    Task<PagedResult<MovieResponseDto>> GetAllAsync(MovieQueryParameters parameters);

    Task<MovieResponseDto?> GetByIdAsync(int id);

    Task<bool> UpdateAsync(int id, UpdateMovieDto dto);

    Task<bool> DeleteAsync(int id);
}
