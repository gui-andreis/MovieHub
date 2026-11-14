using Microsoft.AspNetCore.HttpOverrides;
using MovieHub.Extensions;
using MovieHub.Middleware;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Render/Heroku-like: a plataforma injeta a porta em PORT
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrWhiteSpace(port))
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .MinimumLevel.Override("Polly", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

    builder.Services
        .AddApiControllers()
        .AddDatabase(builder.Configuration)
        .AddAuth(builder.Configuration)
        .AddTokenBlacklist(builder.Configuration)
        .AddApplicationServices(builder.Configuration)
        .AddHttpInfrastructure(builder.Configuration)
        .AddSwagger();

    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });

    var app = builder.Build();

    await app.MigrateDatabaseAsync();
    await app.SeedRolesAndAdminAsync();

    app.UseForwardedHeaders();
    // O log de requisição fica por fora do middleware de exceções para registrar o status final
    app.UseSerilogRequestLogging(o =>
    {
        o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} em {Elapsed:0} ms";
        o.GetLevel = (http, elapsed, ex) => ex != null || http.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose : LogEventLevel.Information;
    });
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseResponseCompression();

    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "MovieHub API v2");
        o.DocumentTitle = "MovieHub API";
    });

    app.UseCors(InfrastructureExtensions.CorsPolicy);
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.UseOutputCache();

    app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    app.MapControllers().RequireRateLimiting("fixed");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "A aplicação falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
