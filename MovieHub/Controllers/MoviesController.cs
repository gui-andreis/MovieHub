namespace MovieHub.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Data.Dtos.Movie;
using MovieHub.Queries.Movies;
using MovieHub.Services.Interfaces;

// Controller responsável pelo gerenciamento de filmes da aplicação.
// Permite consulta pública e operações administrativas restritas a usuários com role "Admin".
[ApiController]
[Route("api/[controller]")]
public class MoviesController : ControllerBase
{
    // Serviço responsável pelas regras de negócio relacionadas a filmes
    private readonly IMovieService _service;

    // Injeta o serviço via Dependency Injection
    public MoviesController(IMovieService service)
    {
        _service = service;
    }

    // Retorna a lista de filmes com suporte a filtros, paginação e ordenação
    // Acesso público (não requer autenticação)
    // Os parâmetros são recebidos via query string
    // Retorna 200 OK com a lista paginada/filtrada
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MovieQueryParameters parameters)
    {
        var result = await _service.GetAllAsync(parameters);
        return Ok(result);
    }

    // Retorna os detalhes de um filme específico pelo ID
    // Acesso público
    // Retorna 200 OK com o filme encontrado
    // Retorna 404 NotFound caso o filme não exista
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var movie = await _service.GetByIdAsync(id);
        if (movie == null)
            return NotFound();
        return Ok(movie);
    }

    // Cria um novo filme no sistema
    // Acesso restrito a usuários com role "Admin"
    // Recebe os dados via DTO no corpo da requisição
    // Retorna 201 Created com o recurso criado
    // Inclui no header a localização do novo recurso (Location)
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateMovieDto dto)
    {
        var result = await _service.CreateAsync(dto);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // Atualiza os dados de um filme existente
    // Acesso restrito a usuários com role "Admin"
    // Recebe o ID via rota e os dados atualizados via DTO
    // Retorna 204 NoContent se atualizado com sucesso
    // Retorna 404 NotFound caso o filme não exista
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateMovieDto dto)
    {
        var success = await _service.UpdateAsync(id, dto);

        if (!success)
            return NotFound();

        return NoContent();
    }

    // Remove um filme do sistema
    // Acesso restrito a usuários com role "Admin"
    // Retorna 204 NoContent se removido com sucesso
    // Retorna 404 NotFound caso o filme não exista
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }
}