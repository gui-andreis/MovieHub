namespace MovieHub.Services.Interfaces;

using MovieHub.Data.Dtos.Movie;
using MovieHub.Pagination;
using MovieHub.Queries.Movies;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;


public interface IMovieService
{
    // Cria um novo filme
    Task<MovieResponseDto> CreateAsync(CreateMovieDto dto);

    // Retorna lista paginada de filmes com base nos parâmetros de consulta
    Task<PagedResult<MovieResponseDto>> GetAllAsync(MovieQueryParameters parameters);

    // Busca um filme pelo Id
    Task<MovieResponseDto?> GetByIdAsync(int id);

    // Atualiza dados de um filme existente 
    Task<bool> UpdateAsync(int id, UpdateMovieDto dto);

    // Remove um filme
    Task<bool> DeleteAsync(int id);
}
