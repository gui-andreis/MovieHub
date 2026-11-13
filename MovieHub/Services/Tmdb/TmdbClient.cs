using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace MovieHub.Services.Tmdb;

public class TmdbOptions
{
    public const string Section = "Tmdb";

    /// <summary>"API Read Access Token" (v4, começa com eyJ...). Também aceita a API key v3.</summary>
    public string? ReadAccessToken { get; set; }

    public string Language { get; set; } = "pt-BR";
    public string FallbackLanguage { get; set; } = "en-US";
    public string Region { get; set; } = "BR";
    public int SeedMovies { get; set; } = 300;
    public int SeedSeries { get; set; } = 120;
    public bool AutoSeed { get; set; } = true;

    /// <summary>Máximo de temporadas com episódios importados por série.</summary>
    public int MaxSeasonsWithEpisodes { get; set; } = 10;

    public int RequestsPerSecond { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 6;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ReadAccessToken);
    public bool IsV3Key => IsConfigured && !ReadAccessToken!.StartsWith("eyJ", StringComparison.Ordinal);
}

public static class TmdbUrls
{
    private const string Base = "https://image.tmdb.org/t/p/";

    public static string? Url(string? path, string size) => string.IsNullOrWhiteSpace(path) ? null : $"{Base}{size}{path}";
    public static string? Poster(string? path) => Url(path, "w500");
    public static string? Backdrop(string? path) => Url(path, "w1280");
    public static string? Logo(string? path) => Url(path, "w500");
    public static string? Profile(string? path) => Url(path, "w185");
    public static string? Still(string? path) => Url(path, "w300");
    public static string? ProviderLogo(string? path) => Url(path, "w154");

    public static DateOnly? ParseDate(string? value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}

/// <summary>Cliente HTTP do TMDB (v3 REST), com throttling (token bucket) e resiliência configurados no DI.</summary>
public class TmdbClient
{
    public const string HttpClientName = "tmdb";

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly IHttpClientFactory _factory;
    private readonly TmdbOptions _options;
    private readonly ILogger<TmdbClient> _logger;

    public TmdbClient(IHttpClientFactory factory, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public TmdbOptions Options => _options;

    public async Task<T?> GetAsync<T>(string path, IDictionary<string, string?>? query, CancellationToken ct) where T : class
    {
        if (!_options.IsConfigured) return null;

        var qs = new List<string>();
        if (query != null)
            qs.AddRange(query.Where(kv => kv.Value != null).Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));
        if (_options.IsV3Key) qs.Add($"api_key={_options.ReadAccessToken}");
        var url = qs.Count == 0 ? path : $"{path}?{string.Join('&', qs)}";

        var client = _factory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!_options.IsV3Key)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ReadAccessToken);

        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("TMDB recusou a credencial (401). Verifique Tmdb__ReadAccessToken.");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct);
    }

    public Task<TmdbPage<TmdbListItem>?> GetListAsync(string path, int page, CancellationToken ct)
        => GetAsync<TmdbPage<TmdbListItem>>(path, new Dictionary<string, string?>
        {
            ["language"] = _options.Language,
            ["region"] = _options.Region,
            ["page"] = page.ToString(CultureInfo.InvariantCulture)
        }, ct);

    public Task<TmdbGenreList?> GetGenresAsync(string kind, CancellationToken ct)
        => GetAsync<TmdbGenreList>($"genre/{kind}/list", new Dictionary<string, string?> { ["language"] = _options.Language }, ct);

    private Dictionary<string, string?> DetailQuery(string append, string? language = null) => new()
    {
        ["language"] = language ?? _options.Language,
        ["append_to_response"] = append,
        ["include_image_language"] = "pt,en,null",
        ["include_video_language"] = "pt,en,null"
    };

    public Task<TmdbMovieDetail?> GetMovieAsync(int id, CancellationToken ct)
        => GetAsync<TmdbMovieDetail>($"movie/{id}", DetailQuery("credits,videos,images,release_dates,watch/providers,keywords"), ct);

    public Task<TmdbTvDetail?> GetTvAsync(int id, CancellationToken ct)
        => GetAsync<TmdbTvDetail>($"tv/{id}", DetailQuery("credits,videos,images,content_ratings,watch/providers,keywords"), ct);

    /// <summary>Busca só textos (sinopse/tagline) em outro idioma — usado como fallback.</summary>
    public async Task<(string? Overview, string? Tagline)> GetFallbackTextAsync(string kind, int id, CancellationToken ct)
    {
        var detail = await GetAsync<TmdbMovieDetail>($"{kind}/{id}", new Dictionary<string, string?> { ["language"] = _options.FallbackLanguage }, ct);
        return (detail?.Overview, detail?.Tagline);
    }

    public Task<TmdbSeasonDetail?> GetSeasonAsync(int tvId, int seasonNumber, CancellationToken ct)
        => GetAsync<TmdbSeasonDetail>($"tv/{tvId}/season/{seasonNumber}", new Dictionary<string, string?> { ["language"] = _options.Language }, ct);

    public Task<TmdbCollection?> GetCollectionAsync(int id, CancellationToken ct)
        => GetAsync<TmdbCollection>($"collection/{id}", new Dictionary<string, string?> { ["language"] = _options.Language }, ct);

    public async Task<TmdbPerson?> GetPersonAsync(int id, CancellationToken ct)
    {
        var person = await GetAsync<TmdbPerson>($"person/{id}", new Dictionary<string, string?> { ["language"] = _options.Language }, ct);
        if (person != null && string.IsNullOrWhiteSpace(person.Biography))
        {
            var en = await GetAsync<TmdbPerson>($"person/{id}", new Dictionary<string, string?> { ["language"] = _options.FallbackLanguage }, ct);
            person.Biography = en?.Biography;
        }
        return person;
    }
}

/// <summary>Limita a taxa de requisições ao TMDB (token bucket compartilhado por todo o processo).</summary>
public sealed class TmdbThrottlingHandler : DelegatingHandler
{
    private static TokenBucketRateLimiter? _limiter;
    private static readonly object Sync = new();

    public TmdbThrottlingHandler(IOptions<TmdbOptions> options)
    {
        lock (Sync)
        {
            var rps = Math.Max(1, options.Value.RequestsPerSecond);
            _limiter ??= new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = rps,
                TokensPerPeriod = rps,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 10_000,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var lease = await _limiter!.AcquireAsync(1, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
