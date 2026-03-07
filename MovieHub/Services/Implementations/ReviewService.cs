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
    public async Task CreateAsync(CreateReviewDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var movieExists = await _context.Movies
            .AnyAsync(m => m.Id == dto.MovieId, cancellationToken);
        if (!movieExists)
            throw new NotFoundException("Filme não encontrado.");

        var alreadyReviewed = await _context.Reviews
            .AnyAsync(r => r.MovieId == dto.MovieId && r.UserId == userId, cancellationToken);
        if (alreadyReviewed)
            throw new ConflictException("Você já avaliou este filme.");

        var review = _mapper.Map<Review>(dto); // Converte DTO para entidade
		review.UserId = userId; // Associa review ao usuário autenticado 
		review.CreatedAt = DateTime.UtcNow; // Define data de criação em UTC

		_context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Retorna todas as reviews de um filme específico
    public async Task<IEnumerable<ReviewResponseDto>> GetAllByMovieAsync(int movieId, CancellationToken cancellationToken = default)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.MovieId == movieId)
            .ToListAsync(cancellationToken);

        return _mapper.Map<IEnumerable<ReviewResponseDto>>(reviews); // Converte para DTO
	}

    // Retorna todas as reviews do usuário autenticado
    public async Task<IEnumerable<ReviewResponseDto>> GetMyReviewsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        return _mapper.Map<IEnumerable<ReviewResponseDto>>(reviews); // Converte para DTO
	}

    // Atualiza uma review existent
    public async Task UpdateAsync(int id, UpdateReviewDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var review = await _context.Reviews.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Review com id {id} não encontrada.");

        if (review.UserId != userId)
            throw new ForbiddenException("Você não pode editar a review de outro usuário.");

        _mapper.Map(dto, review);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Remove uma review (admin pode remover qualquer uma)
    public async Task DeleteAsync(int id, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var review = await _context.Reviews.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Review com id {id} não encontrada.");

        if (!isAdmin && review.UserId != userId)
            throw new ForbiddenException("Você não pode deletar a review de outro usuário.");

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);
    }

}