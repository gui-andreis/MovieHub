using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Data.Dtos.Review;
using MovieHub.Exceptions;
using MovieHub.Models;
using MovieHub.Pagination;
using MovieHub.Queries.Reviews;
using MovieHub.Services.Interfaces;

namespace MovieHub.Services.Implementations;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public ReviewService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

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

        var review = _mapper.Map<Review>(dto);
        review.UserId = userId;
        review.CreatedAt = DateTime.UtcNow;

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ReviewResponseDto>> GetMyReviewsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        return _mapper.Map<IEnumerable<ReviewResponseDto>>(reviews);
    }

    public async Task UpdateAsync(int id, UpdateReviewDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var review = await _context.Reviews.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Review com id {id} não encontrada.");

        if (review.UserId != userId)
            throw new ForbiddenException("Você não pode editar a review de outro usuário.");

        _mapper.Map(dto, review);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var review = await _context.Reviews.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Review com id {id} não encontrada.");

        if (!isAdmin && review.UserId != userId)
            throw new ForbiddenException("Você não pode deletar a review de outro usuário.");

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<ReviewResponseDto>> GetAllByMovieAsync(int movieId, ReviewQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.Reviews
            .Include(r => r.User)
            .Where(r => r.MovieId == movieId)
            .AsQueryable();

        query = parameters.OrderBy?.ToLower() switch
        {
            "oldest" => query.OrderBy(m => m.CreatedAt),
            "rating_desc" => query.OrderByDescending(m => m.Rating),
            "rating_asc" => query.OrderBy(m => m.Rating),
            _ => query.OrderByDescending(m => m.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)parameters.PageSize);

        var reviews = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReviewResponseDto>
        {
            Data = _mapper.Map<List<ReviewResponseDto>>(reviews),
            CurrentPage = parameters.PageNumber,
            PageSize = parameters.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}