using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using MovieHub.Extensions;
using MovieHub.Services.Catalog;

namespace MovieHub.Controllers;

/// <summary>Catálogo público. Personaliza (maturidade, "me") quando há Bearer + X-Profile-Id.</summary>
[Route("api")]
[OutputCache(PolicyName = CachePolicies.Catalog)]
public class CatalogController : AppControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly IBrowseService _browse;

    public CatalogController(ICatalogService catalog, IBrowseService browse)
    {
        _catalog = catalog;
        _browse = browse;
    }

    /// <summary>Home: hero + linhas (continuar assistindo, minha lista, top 10, gêneros…).</summary>
    [HttpGet("browse/home")]
    public async Task<IActionResult> Home([FromQuery] string? type, CancellationToken ct)
        => Ok(await _browse.GetHomeAsync(type, ct));

    /// <summary>Lista paginada com filtros. sort: popularity | rating | release_desc | release_asc | name | added</summary>
    [HttpGet("titles")]
    public async Task<IActionResult> List([FromQuery] TitleQueryParameters query, CancellationToken ct)
        => Ok(await _catalog.ListAsync(query, ct), "Títulos recuperados com sucesso.");

    [HttpGet("titles/trending")]
    public async Task<IActionResult> Trending([FromQuery] string? type, [FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _catalog.GetTrendingAsync(type, limit, ct));

    [HttpGet("titles/top10")]
    public async Task<IActionResult> Top10([FromQuery] string? type, CancellationToken ct)
        => Ok(await _catalog.GetTop10Async(type, ct));

    /// <summary>"Surpreenda-me": um título bem avaliado aleatório.</summary>
    [HttpGet("titles/random")]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Random([FromQuery] string? type, [FromQuery] int? genreId, CancellationToken ct)
        => Ok(await _catalog.GetRandomAsync(type, genreId, ct));

    [HttpGet("titles/upcoming")]
    public async Task<IActionResult> Upcoming([FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _catalog.GetUpcomingAsync(limit, ct));

    [HttpGet("titles/{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Ok(await _catalog.GetDetailAsync(id, ct));

    [HttpGet("titles/{id:int}/similar")]
    public async Task<IActionResult> Similar(int id, [FromQuery] int limit = 12, CancellationToken ct = default)
        => Ok(await _catalog.GetSimilarAsync(id, limit, ct));

    [HttpGet("titles/{id:int}/seasons/{seasonNumber:int}")]
    public async Task<IActionResult> Season(int id, int seasonNumber, CancellationToken ct)
        => Ok(await _catalog.GetSeasonAsync(id, seasonNumber, ct));

    /// <summary>Busca sem acento por títulos e pessoas.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
        => Ok(await _catalog.SearchAsync(q, ct));

    [HttpGet("genres")]
    public async Task<IActionResult> Genres(CancellationToken ct)
        => Ok(await _catalog.GetGenresAsync(ct));

    [HttpGet("genres/{slug}/titles")]
    public async Task<IActionResult> GenreTitles(string slug, [FromQuery] TitleQueryParameters query, CancellationToken ct)
        => Ok(await _catalog.GetGenreTitlesAsync(slug, query, ct));

    [HttpGet("people/{id:int}")]
    public async Task<IActionResult> Person(int id, CancellationToken ct)
        => Ok(await _catalog.GetPersonAsync(id, ct));

    [HttpGet("collections/{id:int}")]
    public async Task<IActionResult> Collection(int id, CancellationToken ct)
        => Ok(await _catalog.GetCollectionAsync(id, ct));

    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken ct)
        => Ok(await _catalog.GetProvidersAsync(ct));
}
