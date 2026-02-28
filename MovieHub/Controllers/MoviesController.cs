namespace MovieHub.Controllers;

using Microsoft.AspNetCore.Mvc;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Services.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class MoviesController : ControllerBase
{
    private readonly IMovieService _service;

    public MoviesController(IMovieService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var movies = await _service.GetAllAsync();
        return Ok(movies);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var movie = await _service.GetByIdAsync(id);

        if (movie == null)
            return NotFound();

        return Ok(movie);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMovieDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateMovieDto dto)
    {
        var success = await _service.UpdateAsync(id, dto);

        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);

        if (!success)
            return NotFound();

        return NoContent();
    }
}
