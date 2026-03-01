using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Models;
using MovieHub.Queries.Movies;
using MovieHub.Services.Interfaces;
using MovieHub.Pagination;

namespace MovieHub.Services.Implementations;

public class MovieService : IMovieService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

	// Injeção de dependências do DbContext e AutoMapper
	public MovieService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

	// Cria um novo filme no banco de dados
	public async Task<MovieResponseDto> CreateAsync(CreateMovieDto dto)
    {
        var movie = _mapper.Map<Movie>(dto); // Mapeia DTO para entidade 

		_context.Movies.Add(movie);
        await _context.SaveChangesAsync();

        return _mapper.Map<MovieResponseDto>(movie); // Retorna DTO de resposta
	}

	// Busca filme por Id incluindo suas reviews
	public async Task<MovieResponseDto?> GetByIdAsync(int id)
    {
        var movie = await _context.Movies
            .Include(m => m.Reviews)  // Carrega relacionamento de avaliações
			.FirstOrDefaultAsync(m => m.Id == id);
        if (movie == null)
            return null; // Retorna null caso não encontre

		return _mapper.Map<MovieResponseDto>(movie); // Converte para DTO
	}

	// Atualiza dados de um filme existente
	public async Task<bool> UpdateAsync(int id, UpdateMovieDto dto)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie == null)
            return false;

        _mapper.Map(dto, movie);

        await _context.SaveChangesAsync();
        return true;
    }

	// Remove um filme do banco
	public async Task<bool> DeleteAsync(int id)
    {
        var movie = await _context.Movies.FindAsync(id);

        if (movie == null)
            return false;

        _context.Movies.Remove(movie);
        await _context.SaveChangesAsync();

        return true;
    }

	// Retorna lista paginada de filmes
	public async Task<PagedResult<MovieResponseDto>> GetAllAsync(MovieQueryParameters parameters)
    {
        var query = _context.Movies.AsQueryable();

        var totalCount = await query.CountAsync(); // Total de registros antes da paginação

		var movies = await query
            .Include(m => m.Reviews) // Carrega avaliações para cada filme
			.Skip((parameters.PageNumber - 1) * parameters.PageSize) // Pula registros anteriores
			.Take(parameters.PageSize) // Limita quantidade por página
			.ToListAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)parameters.PageSize); // Calcula total de páginas

		var movieDtos = _mapper.Map<List<MovieResponseDto>>(movies);// Converte lista para DTO

		return new PagedResult<MovieResponseDto>
        {
            Data = movieDtos,
            CurrentPage = parameters.PageNumber,
            PageSize = parameters.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}