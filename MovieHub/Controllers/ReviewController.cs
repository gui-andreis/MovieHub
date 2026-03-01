using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MovieHub.Data.Dtos.Review;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateReview(CreateReviewDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        await _reviewService.CreateAsync(dto, userId);
        return Ok("Review criada com sucesso.");
    }

    [HttpGet("movie/{movieId}")]
    public async Task<IActionResult> GetAllByMovie(int movieId)
    {
        var reviews = await _reviewService.GetAllByMovieAsync(movieId);
        return Ok(reviews);
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyReviews()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        var reviews = await _reviewService.GetMyReviewsAsync(userId);
        return Ok(reviews);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateReviewDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        var success = await _reviewService.UpdateAsync(id, dto, userId);
        if (!success)
            return NotFound();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        // Verifica se é admin — admin pode deletar qualquer review
        var isAdmin = User.IsInRole("Admin");

        var success = await _reviewService.DeleteAsync(id, userId, isAdmin);
        if (!success)
            return NotFound();
        return NoContent();
    }
}