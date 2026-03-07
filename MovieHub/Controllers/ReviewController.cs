using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Data.Dtos.Review;
using MovieHub.Services.Interfaces;
using System.Security.Claims;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : AppControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateReview(CreateReviewDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _reviewService.CreateAsync(dto, userId, cancellationToken);
        return Ok("Review criada com sucesso.");
    }

    [HttpGet("movie/{movieId}")]
    public async Task<IActionResult> GetAllByMovie(int movieId, CancellationToken cancellationToken)
    {
        var reviews = await _reviewService.GetAllByMovieAsync(movieId, cancellationToken);
        return Ok(reviews, "Reviews recuperadas com sucesso.");
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyReviews(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var reviews = await _reviewService.GetMyReviewsAsync(userId, cancellationToken);
        return Ok(reviews, "Reviews recuperadas com sucesso.");
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateReviewDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _reviewService.UpdateAsync(id, dto, userId, cancellationToken);
        return NoContent("Review atualizada com sucesso.");
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole("Admin");
        await _reviewService.DeleteAsync(id, userId, isAdmin, cancellationToken);
        return NoContent("Review removida com sucesso.");
    }
}