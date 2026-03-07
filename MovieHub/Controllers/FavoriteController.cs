using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MovieHub.Data.Dtos.Favorite;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoriteController : AppControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoriteController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddFavoriteDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _favoriteService.AddAsync(dto.MovieId, userId, cancellationToken);
        return NoContent("Filme adicionado aos favoritos.");
    }

    [HttpDelete("{movieId}")]
    public async Task<IActionResult> Remove(int movieId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _favoriteService.RemoveAsync(movieId, userId, cancellationToken);
        return NoContent("Favorito removido com sucesso.");
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyFavorites(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var favorites = await _favoriteService.GetMyFavoritesAsync(userId, cancellationToken);
        return Ok(favorites, "Favoritos recuperados com sucesso.");
    }
}