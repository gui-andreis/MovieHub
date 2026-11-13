using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Dtos;
using MovieHub.Services.Catalog;

namespace MovieHub.Controllers;

[Route("api")]
public class ReviewsController : AppControllerBase
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    /// <summary>sort: newest (padrão) | oldest | rating_desc | rating_asc</summary>
    [HttpGet("titles/{titleId:int}/reviews")]
    public async Task<IActionResult> ForTitle(int titleId, [FromQuery] ReviewQueryParameters query, CancellationToken ct)
        => Ok(await _reviews.GetForTitleAsync(titleId, query, ct), "Reviews recuperadas com sucesso.");

    [HttpGet("titles/{titleId:int}/reviews/summary")]
    public async Task<IActionResult> Summary(int titleId, CancellationToken ct)
        => Ok(await _reviews.GetSummaryAsync(titleId, ct));

    [Authorize]
    [HttpPost("titles/{titleId:int}/reviews")]
    public async Task<IActionResult> Create(int titleId, [FromBody] ReviewRequest request, CancellationToken ct)
        => Created(await _reviews.CreateAsync(titleId, request, ct), "Avaliação publicada.");

    [Authorize]
    [HttpPut("reviews/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ReviewRequest request, CancellationToken ct)
        => Ok(await _reviews.UpdateAsync(id, request, ct), "Avaliação atualizada.");

    [Authorize]
    [HttpDelete("reviews/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _reviews.DeleteAsync(id, ct);
        return Done("Avaliação removida.");
    }
}
