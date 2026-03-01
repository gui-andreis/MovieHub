using AutoMapper;
using MovieHub.Data;
using MovieHub.Data.Dtos.Review;
using MovieHub.Models;
using MovieHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MovieHub.Exceptions;

namespace MovieHub.Services.Implementos;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public ReviewService(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task CreateAsync(CreateReviewDto dto, string userId)
    {
        var movieExists = await _context.Movies
            .AnyAsync(m => m.Id == dto.MovieId);

        if (!movieExists)
            throw new Exception("Filme não encontrado.");

        var review = _mapper.Map<Review>(dto);
        review.UserId = userId;
        review.CreatedAt = DateTime.UtcNow;

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ReviewResponseDto>> GetAllByMovieAsync(int movieId)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.MovieId == movieId)
            .ToListAsync();

        return _mapper.Map<IEnumerable<ReviewResponseDto>>(reviews);
    }


    public async Task<IEnumerable<ReviewResponseDto>> GetMyReviewsAsync(string userId)
    {
        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.UserId == userId)
            .ToListAsync();

        return _mapper.Map<IEnumerable<ReviewResponseDto>>(reviews);
    }

    public async Task<bool> UpdateAsync(int id, UpdateReviewDto dto, string userId)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return false;

        if (review.UserId != userId)
            throw new NotFoundException("Filme não encontrado.");

        _mapper.Map(dto, review);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, string userId, bool isAdmin)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review == null)
            return false;

        // Admin pode deletar qualquer review, user só a própria
        if (!isAdmin && review.UserId != userId)
            throw new ForbiddenException("Você não pode deletar a review de outro usuário.");

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();
        return true;
    }

}