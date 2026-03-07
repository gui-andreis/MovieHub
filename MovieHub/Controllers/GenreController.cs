using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Data.Dtos.Genre;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GenreController : AppControllerBase
{
    private readonly IGenreService _genreService;

    public GenreController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var genres = await _genreService.GetAllAsync(cancellationToken);
        return Ok(genres, "Gêneros recuperados com sucesso.");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(GenreRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _genreService.CreateAsync(dto, cancellationToken);
        return Ok(result, "Gênero criado com sucesso.");
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _genreService.DeleteAsync(id, cancellationToken);
        return NoContent("Gênero removido com sucesso.");
    }
}