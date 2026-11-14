using System.Threading.Channels;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieHub.Data;

namespace MovieHub.Services.Tmdb;

public record ImportStatusDto(
    bool IsRunning,
    string State,
    string? Source,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    int Total,
    int Processed,
    int Imported,
    int Skipped,
    int Failed,
    string? Message,
    string? LastError,
    bool TmdbConfigured);

/// <summary>Estado da importação em andamento (singleton, thread-safe).</summary>
public class ImportStatusTracker
{
    private readonly object _sync = new();
    private string _state = "idle";
    private string? _source;
    private DateTime? _startedAt, _finishedAt;
    private int _total, _imported, _skipped, _failed;
    private string? _message, _lastError;

    public bool IsRunning { get { lock (_sync) return _state is "queued" or "running"; } }

    public bool TmdbConfigured { get; set; }

    public void Queue(string source)
    {
        lock (_sync)
        {
            _state = "queued";
            _source = source;
            _message = "Importação na fila.";
        }
    }

    public void Start(ImportJob job)
    {
        lock (_sync)
        {
            _state = "running";
            _source = job.Source;
            _startedAt = DateTime.UtcNow;
            _finishedAt = null;
            _total = _imported = _skipped = _failed = 0;
            _message = "Buscando listas no TMDB…";
            _lastError = null;
        }
    }

    public void SetTotal(int total) { lock (_sync) { _total = total; _message = "Importando títulos…"; } }
    public void Imported() { lock (_sync) _imported++; }
    public void Skipped() { lock (_sync) _skipped++; }
    public void Failed(string error) { lock (_sync) { _failed++; _lastError = error; } }

    public void Complete(string message)
    {
        lock (_sync) { _state = "completed"; _finishedAt = DateTime.UtcNow; _message = message; }
    }

    public void Fail(string message)
    {
        lock (_sync) { _state = "failed"; _finishedAt = DateTime.UtcNow; _message = message; _lastError ??= message; }
    }

    public ImportStatusDto Snapshot()
    {
        lock (_sync)
        {
            return new ImportStatusDto(_state is "queued" or "running", _state, _source, _startedAt, _finishedAt, _total,
                _imported + _skipped + _failed, _imported, _skipped, _failed, _message, _lastError, TmdbConfigured);
        }
    }
}

/// <summary>Fila de jobs de importação consumida pelo <see cref="ImportBackgroundService"/>.</summary>
public class ImportQueue
{
    private readonly Channel<ImportJob> _channel = Channel.CreateBounded<ImportJob>(new BoundedChannelOptions(4)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public bool TryEnqueue(ImportJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<ImportJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Executa importações do TMDB fora do ciclo de requisição. Na subida, se o catálogo estiver vazio
/// e houver token, agenda o seed automático.
/// </summary>
public class ImportBackgroundService : BackgroundService
{
    private readonly ImportQueue _queue;
    private readonly ITmdbImporter _importer;
    private readonly ImportStatusTracker _status;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutputCacheStore _cache;
    private readonly TmdbOptions _options;
    private readonly ILogger<ImportBackgroundService> _logger;

    public ImportBackgroundService(
        ImportQueue queue,
        ITmdbImporter importer,
        ImportStatusTracker status,
        IServiceScopeFactory scopeFactory,
        IOutputCacheStore cache,
        IOptions<TmdbOptions> options,
        ILogger<ImportBackgroundService> logger)
    {
        _queue = queue;
        _importer = importer;
        _status = status;
        _scopeFactory = scopeFactory;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _status.TmdbConfigured = _importer.IsConfigured;
        await Task.Yield();

        try
        {
            await ScheduleSeedIfNeededAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Não foi possível verificar a necessidade de seed do catálogo");
        }

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _importer.ImportCatalogAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job de importação falhou");
            }
            finally
            {
                await _cache.EvictByTagAsync(CacheTags.Catalog, CancellationToken.None);
            }
        }
    }

    private async Task ScheduleSeedIfNeededAsync(CancellationToken ct)
    {
        if (!_importer.IsConfigured)
        {
            _logger.LogWarning(
                "Tmdb__ReadAccessToken não configurado: o catálogo não será populado automaticamente. " +
                "Configure o token e reinicie, ou chame POST /api/admin/import como Admin.");
            return;
        }

        if (!_options.AutoSeed)
        {
            _logger.LogInformation("Seed automático do TMDB desativado (Tmdb__AutoSeed=false).");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Titles.AnyAsync(ct))
        {
            _logger.LogInformation("Catálogo já possui títulos — seed automático ignorado.");
            return;
        }

        _logger.LogInformation("Catálogo vazio: agendando seed do TMDB ({Movies} filmes, {Series} séries)", _options.SeedMovies, _options.SeedSeries);
        _status.Queue("startup-seed");
        _queue.TryEnqueue(new ImportJob(true, true, _options.SeedMovies, _options.SeedSeries, "startup-seed"));
    }
}

public static class CacheTags
{
    public const string Catalog = "catalog";
}
