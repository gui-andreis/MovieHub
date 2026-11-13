using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Dtos;
using MovieHub.Services.Catalog;

namespace MovieHub.Controllers;

/// <summary>Dados pessoais do perfil ativo (exige Bearer + X-Profile-Id).</summary>
[Authorize]
[Route("api/me")]
public class MeController : AppControllerBase
{
    private readonly IMeService _me;

    public MeController(IMeService me) => _me = me;

    [HttpGet("list")]
    public async Task<IActionResult> MyList(CancellationToken ct) => Ok(await _me.GetMyListAsync(ct));

    [HttpPut("list/{titleId:int}")]
    public async Task<IActionResult> AddToList(int titleId, CancellationToken ct)
        => Ok(await _me.AddToListAsync(titleId, ct), "Adicionado à Minha lista.");

    [HttpDelete("list/{titleId:int}")]
    public async Task<IActionResult> RemoveFromList(int titleId, CancellationToken ct)
        => Ok(await _me.RemoveFromListAsync(titleId, ct), "Removido da Minha lista.");

    [HttpGet("continue-watching")]
    public async Task<IActionResult> ContinueWatching(CancellationToken ct) => Ok(await _me.GetContinueWatchingAsync(ct));

    /// <summary>Salva o progresso. A partir de 90% o item é marcado como concluído e, em séries, o próximo episódio entra em "continuar assistindo".</summary>
    [HttpPut("progress")]
    public async Task<IActionResult> SaveProgress([FromBody] ProgressRequest request, CancellationToken ct)
        => Ok(await _me.SaveProgressAsync(request, ct), "Progresso salvo.");

    [HttpDelete("progress/{titleId:int}")]
    public async Task<IActionResult> RemoveProgress(int titleId, CancellationToken ct)
    {
        await _me.RemoveProgressAsync(titleId, ct);
        return Done("Removido de Continuar assistindo.");
    }

    [HttpPut("reactions/{titleId:int}")]
    public async Task<IActionResult> SetReaction(int titleId, [FromBody] ReactionRequest request, CancellationToken ct)
        => Ok(await _me.SetReactionAsync(titleId, request.Value!.Value, ct), "Avaliação registrada.");

    [HttpDelete("reactions/{titleId:int}")]
    public async Task<IActionResult> RemoveReaction(int titleId, CancellationToken ct)
        => Ok(await _me.RemoveReactionAsync(titleId, ct), "Avaliação removida.");

    [HttpGet("reviews")]
    public async Task<IActionResult> MyReviews(CancellationToken ct) => Ok(await _me.GetMyReviewsAsync(ct));

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(await _me.GetStatsAsync(ct));
}
