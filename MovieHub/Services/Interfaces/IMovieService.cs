namespace MovieHub.Services.Interfaces;

using MovieHub.Data.Dtos.Movie;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IMovieService
{
    Task<MovieResponseDto> CreateAsync(CreateMovieDto dto);

    Task<IEnumerable<MovieResponseDto>> GetAllAsync();

    Task<MovieResponseDto?> GetByIdAsync(int id);

    Task<bool> UpdateAsync(int id, UpdateMovieDto dto);

    Task<bool> DeleteAsync(int id);
}
