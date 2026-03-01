using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Data.Dtos.Review;
using MovieHub.Services.Interfaces;
using System.Data;
using System.Security.Claims;

namespace MovieHub.Controllers;



// Controller responsável pelo gerenciamento de avaliações (reviews) de filmes.
// Permite criação, consulta, atualização e exclusão de reviews.
// Algumas operações exigem autenticação e respeitam regras de autorização (owner/admin).
[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    // Serviço responsável pelas regras de negócio relacionadas a reviews
    private readonly IReviewService _reviewService;

    // Injeta o serviço via Dependency Injection
    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    // Cria uma nova review para um filme
    // Requer autenticação
    // O usuário autenticado será associado como autor da review
    // Retorna 200 OK se criada com sucesso
    // Retorna 401 Unauthorized caso o token seja inválido
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

    // Retorna todas as reviews associadas a um filme específico
    // Acesso público
    // Retorna 200 OK com a lista de avaliações
    [HttpGet("movie/{movieId}")]
    public async Task<IActionResult> GetAllByMovie(int movieId)
    {
        var reviews = await _reviewService.GetAllByMovieAsync(movieId);

        return Ok(reviews);
    }

    // Retorna todas as reviews criadas pelo usuário autenticado
    // Requer autenticação
    // Retorna 200 OK com a lista
    // Retorna 401 Unauthorized caso o token seja inválido
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

    // Atualiza uma review existente
    // Requer autenticação
    // Apenas o autor da review pode atualizar
    // Retorna 204 NoContent se atualizada com sucesso
    // Retorna 404 NotFound caso a review não exista ou não pertença ao usuário
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

    // Remove uma review existente
    // Requer autenticação
    // O autor pode deletar sua própria review
    // Usuários com role "Admin" podem deletar qualquer review
    // Retorna 204 NoContent se removida com sucesso
    // Retorna 404 NotFound caso a review não exista ou o usuário não tenha permissão
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

        // verifica se o usuário possui role de administrador
        var isAdmin = User.IsInRole("Admin");

        var success = await _reviewService.DeleteAsync(id, userId, isAdmin);
        if (!success)
            return NotFound();

        return NoContent();
    }
}