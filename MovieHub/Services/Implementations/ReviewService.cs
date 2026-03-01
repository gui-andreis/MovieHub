using AutoMapper;
using MovieHub.Data;
using MovieHub.Data.Dtos.Review;
using MovieHub.Models;
using MovieHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MovieHub.Exceptions;

namespace MovieHub.Services.Implementations;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

	// Injeção de dependências do DbContext e AutoMapper
	public ReviewService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

	// Cria uma nova review para um filme
	public async Task CreateAsync(CreateReviewDto dto, string userId)
    {
        var movieExists = await _context.Movies
            .AnyAsync(m => m.Id == dto.MovieId); // Verifica se o filme existe

		if (!movieExists)
            throw new NotFoundException("Filme não encontrado."); ; 

        var review = _mapper.Map<Review>(dto); // Converte DTO para entidade
		review.UserId = userId; // Associa review ao usuário autenticado 
		review.CreatedAt = DateTime.UtcNow; // Define data de criação em UTC

		_context.Reviews.Add(review);
        await _context.SaveChangesAsync();
    }

	// Retorna todas as reviews de um filme específico
	public async Task<IEnumerable<ReviewResponseDto>> GetAllByMovieAsync(int movieId)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User) // Carrega dados do usuário que escreveu a review
			.Where(r => r.MovieId == movieId)
            .ToListAsync();

        return _mapper.Map<IEnumerable<ReviewResponseDto>>(reviews); // Converte para DTO
	}

	// Retorna todas as reviews do usuário autenticado
	public async Task<IEnumerable<ReviewResponseDto>> GetMyReviewsAsync(string userId)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User) // Inclui dados do usuário (caso necessário na resposta)
			.Where(r => r.UserId == userId)
            .ToListAsync();

        return _mapper.Map<IEnumerable<ReviewResponseDto>>(reviews); // Converte para DTO
	}

	// Atualiza uma review existent
	public async Task<bool> UpdateAsync(int id, UpdateReviewDto dto, string userId)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return false;

        if (review.UserId != userId)
			throw new ForbiddenException("Você não pode editar a review de outro usuário."); // Impede usuário de editar review de outro 

		_mapper.Map(dto, review); // Atualiza propriedades da entidade

		await _context.SaveChangesAsync();

        return true;
    }

	// Remove uma review (admin pode remover qualquer uma)
	public async Task<bool> DeleteAsync(int id, string userId, bool isAdmin)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return false;

		// Admin pode deletar qualquer review, já usuário comum apenas a própria
		if (!isAdmin && review.UserId != userId)
            throw new ForbiddenException("Você não pode deletar a review de outro usuário.");

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();// Confirma exclusão

		return true;
    }

}