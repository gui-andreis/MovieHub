using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieHub.Dtos;
using MovieHub.Services.Catalog;

namespace MovieHub.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : AppControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(await _admin.GetStatsAsync(ct));

    /// <summary>Dispara a importação do TMDB em background (acompanhe em GET /api/admin/import/status).</summary>
    [HttpPost("import")]
    public IActionResult Import([FromBody] ImportRequest request)
        => Accepted(_admin.StartImport(request), "Importação agendada.");

    [HttpGet("import/status")]
    public IActionResult ImportStatus() => Ok(_admin.GetImportStatus());

    /// <summary>Importa/atualiza um título específico pelo id do TMDB. type: movie | series</summary>
    [HttpPost("titles/{tmdbId:int}/import")]
    public async Task<IActionResult> ImportTitle(int tmdbId, [FromQuery] string? type, CancellationToken ct)
        => Ok(await _admin.ImportTitleAsync(tmdbId, type, ct), "Título importado.");

    [HttpPost("titles")]
    public async Task<IActionResult> CreateTitle([FromBody] TitleUpsertRequest request, CancellationToken ct)
        => Created(await _admin.CreateTitleAsync(request, ct), "Título criado.");

    [HttpPut("titles/{id:int}")]
    public async Task<IActionResult> UpdateTitle(int id, [FromBody] TitleUpsertRequest request, CancellationToken ct)
        => Ok(await _admin.UpdateTitleAsync(id, request, ct), "Título atualizado.");

    [HttpDelete("titles/{id:int}")]
    public async Task<IActionResult> DeleteTitle(int id, CancellationToken ct)
    {
        await _admin.DeleteTitleAsync(id, ct);
        return Done("Título removido.");
    }

    [HttpPatch("titles/{id:int}/featured")]
    public async Task<IActionResult> Featured(int id, [FromBody] FeaturedRequest request, CancellationToken ct)
        => Ok(await _admin.SetFeaturedAsync(id, request.IsFeatured, ct), request.IsFeatured ? "Título em destaque." : "Destaque removido.");

    [HttpPost("genres")]
    public async Task<IActionResult> CreateGenre([FromBody] GenreRequest request, CancellationToken ct)
        => Created(await _admin.CreateGenreAsync(request, ct), "Gênero criado.");

    [HttpPut("genres/{id:int}")]
    public async Task<IActionResult> UpdateGenre(int id, [FromBody] GenreRequest request, CancellationToken ct)
        => Ok(await _admin.UpdateGenreAsync(id, request, ct), "Gênero atualizado.");

    [HttpDelete("genres/{id:int}")]
    public async Task<IActionResult> DeleteGenre(int id, CancellationToken ct)
    {
        await _admin.DeleteGenreAsync(id, ct);
        return Done("Gênero removido.");
    }
}
