using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Common;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Pagination;
using MovieHub.Queries.Movies;
using MovieHub.Services.Interfaces;

namespace MovieHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MoviesController : AppControllerBase
{
    private readonly IMovieService _service;

    public MoviesController(IMovieService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MovieResponseDto>>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] MovieQueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(parameters, cancellationToken);
        return Ok(result, "Filmes recuperados com sucesso.");
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<MovieResponseDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var movie = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(movie, "Filme recuperado com sucesso.");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MovieResponseDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Create([FromForm] CreateMovieDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(dto, cancellationToken);
        return Created(result, nameof(GetById), new { id = result.Id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateMovieDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, dto, cancellationToken);
        return NoContent("Filme atualizado com sucesso.");
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent("Filme removido com sucesso.");
    }
}