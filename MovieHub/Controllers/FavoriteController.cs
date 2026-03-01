using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MovieHub.Data.Dtos.Favorite;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

// Controller responsável por gerenciar os filmes favoritos do usuário autenticado.
// Todas as rotas exigem autenticação via JWT.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoriteController : ControllerBase
{
    // Serviço responsável pelas regras de negócio relacionadas a favoritos
    private readonly IFavoriteService _favoriteService;

    // Injeta o serviço de favoritos via Dependency Injection
    public FavoriteController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    // Adiciona um filme à lista de favoritos do usuário autenticado
    // Recebe o MovieId via DTO no corpo da requisição
    // Retorna 200 OK se a operação for bem-sucedida
    // Retorna 401 Unauthorized caso o usuário não esteja autenticado
    [HttpPost]
    public async Task<IActionResult> Add(AddFavoriteDto dto)
    {
        // Obtém o ID do usuário autenticado a partir das claims do token JWT
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Caso o token seja inválido ou não contenha a claim esperada
        if (userId == null)
            return Unauthorized();

        // Executa a regra de negócio para adicionar o favorito
        await _favoriteService.AddAsync(dto.MovieId, userId);

        return Ok("Filme adicionado aos favoritos.");    
    }

    // Remove um filme da lista de favoritos do usuário autenticado
    // Recebe o MovieId via parâmetro de rota
    // Retorna 204 NoContent se removido com sucesso
    // Retorna 404 NotFound caso o favorito não exista
    // Retorna 401 Unauthorized caso o usuário não esteja autenticado
    [HttpDelete("{movieId}")]
    public async Task<IActionResult> Remove(int movieId)
    {
        // Obtém o ID do usuário autenticado
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

        // Tenta remover o favorito
        var success = await _favoriteService.RemoveAsync(movieId, userId);


        // Caso o favorito não seja encontrado
        if (!success)
            return NotFound("Favorito não encontrado.");

        return NoContent();
    }

    // Retorna todos os filmes favoritados pelo usuário autenticado
    // Rota: GET api/favorite/my
    // Retorna 200 OK com a lista de favoritos
    // Retorna 401 Unauthorized caso o usuário não esteja autenticado
    [HttpGet("my")]
    public async Task<IActionResult> GetMyFavorites()
    {
        // Obtém o ID do usuário autenticado
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

        // Busca os favoritos do usuário
        var favorites = await _favoriteService.GetMyFavoritesAsync(userId);

        return Ok(favorites);
    }
}