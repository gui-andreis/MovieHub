using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MovieHub.Data.Dtos.Favorite;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoriteController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoriteController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddFavoriteDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        await _favoriteService.AddAsync(dto.MovieId, userId);
        return Ok("Filme adicionado aos favoritos.");    
    }

    [HttpDelete("{movieId}")]
    public async Task<IActionResult> Remove(int movieId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        var success = await _favoriteService.RemoveAsync(movieId, userId);
        if (!success)
            return NotFound("Favorito não encontrado.");

        return NoContent();
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyFavorites()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized();

        var favorites = await _favoriteService.GetMyFavoritesAsync(userId);
        return Ok(favorites);
    }
}