using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MovieHub.Common;
using MovieHub.Data;
using MovieHub.Models;
using MovieHub.Services;
using MovieHub.Services.Auth;
using MovieHub.Services.Catalog;
using MovieHub.Services.Tmdb;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MovieHub.Extensions;

public static class CachePolicies
{
    public const string Catalog = "catalog";
}

public static class InfrastructureExtensions
{
    // ---------------------------------------------------------------- MVC / JSON

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            })
            .ConfigureApiBehaviorOptions(o =>
            {
                // Erros de validação no mesmo envelope: { status: 400, message, data: null, errors: { campo: [...] } }
                o.InvalidModelStateResponseFactory = ctx =>
                {
                    var errors = ctx.ModelState
                        .Where(kv => kv.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kv => string.IsNullOrEmpty(kv.Key) ? "body" : char.ToLowerInvariant(kv.Key[0]) + kv.Key[1..],
                            kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Valor inválido." : e.ErrorMessage).ToArray());
                    return new BadRequestObjectResult(ApiResponse<object>.Error(400, "Dados inválidos.", errors));
                };
            });

        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        return services;
    }

    // ---------------------------------------------------------------- Banco

    /// <summary>
    /// Aceita ConnectionStrings__DefaultConnection (formato Npgsql) ou DATABASE_URL
    /// (postgres://user:pass@host:port/db?sslmode=require — formato do Neon/Render).
    /// </summary>
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return ConvertDatabaseUrl(databaseUrl);

        return configuration.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection ou DATABASE_URL.");
    }

    public static string ConvertDatabaseUrl(string url)
    {
        if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return url; // já está no formato Npgsql

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslMode = query["sslmode"];

        var sb = new StringBuilder();
        sb.Append($"Host={uri.Host};");
        sb.Append($"Port={(uri.Port > 0 ? uri.Port : 5432)};");
        sb.Append($"Database={uri.AbsolutePath.Trim('/')};");
        sb.Append($"Username={Uri.UnescapeDataString(userInfo[0])};");
        if (userInfo.Length > 1) sb.Append($"Password={Uri.UnescapeDataString(userInfo[1])};");
        // Respeita o sslmode da URL (Neon manda sslmode=require); sem ele, tenta SSL e cai para texto puro
        var ssl = sslMode?.ToLowerInvariant() switch
        {
            "disable" => "Disable",
            "allow" => "Allow",
            "require" or "verify-ca" or "verify-full" => "Require",
            _ => "Prefer"
        };
        sb.Append($"SSL Mode={ssl};");
        sb.Append("Trust Server Certificate=true;");
        sb.Append("Maximum Pool Size=20;");
        return sb.ToString();
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = ResolveConnectionString(configuration);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connection, npgsql => npgsql.EnableRetryOnFailure(3)));
        return services;
    }

    // ---------------------------------------------------------------- Identity / JWT

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>();

        var jwtSection = configuration.GetSection(JwtOptions.Section);
        services.Configure<JwtOptions>(jwtSection);
        var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SecretKey) || jwt.SecretKey.Length < 32)
            throw new InvalidOperationException("Jwt__SecretKey precisa ter pelo menos 32 caracteres.");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var blacklist = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
                        var jti = context.Principal?.FindFirst("jti")?.Value;
                        if (jti != null && await blacklist.IsTokenBlacklistedAsync(jti))
                            context.Fail("Token invalidado.");
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Error(401, "Autenticação necessária ou token inválido."));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Error(403, "Você não tem permissão para acessar este recurso."));
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    // ---------------------------------------------------------------- Redis (opcional)

    /// <summary>Converte redis://, rediss:// (Upstash) ou "host:port" em ConfigurationOptions.</summary>
    public static ConfigurationOptions ParseRedis(string value)
    {
        ConfigurationOptions options;
        if (value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(value);
            options = new ConfigurationOptions
            {
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
            };
            options.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                if (parts.Length == 2)
                {
                    if (!string.IsNullOrEmpty(parts[0]) && parts[0] != "default") options.User = Uri.UnescapeDataString(parts[0]);
                    options.Password = Uri.UnescapeDataString(parts[1]);
                }
                else
                {
                    options.Password = Uri.UnescapeDataString(parts[0]);
                }
            }
            if (options.Ssl) options.SslHost = uri.Host;
        }
        else
        {
            options = ConfigurationOptions.Parse(value);
        }

        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 5000;
        options.ConnectRetry = 2;
        return options;
    }

    public static IServiceCollection AddTokenBlacklist(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis") ?? configuration["REDIS_URL"];
        if (string.IsNullOrWhiteSpace(redis))
        {
            Serilog.Log.Warning("Redis não configurado — blacklist de tokens em memória (ok para uma única instância).");
            services.AddSingleton<ITokenBlacklistService, InMemoryTokenBlacklistService>();
            return services;
        }

        try
        {
            var multiplexer = ConnectionMultiplexer.Connect(ParseRedis(redis));
            if (!multiplexer.IsConnected)
                Serilog.Log.Warning("Redis inacessível no momento; tentará reconectar. Blacklist usa memória local como apoio.");
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            services.AddSingleton<ITokenBlacklistService, RedisTokenBlacklistService>();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Falha ao configurar Redis — usando blacklist em memória.");
            services.AddSingleton<ITokenBlacklistService, InMemoryTokenBlacklistService>();
        }
        return services;
    }

    // ---------------------------------------------------------------- Serviços de domínio

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICurrentProfile, CurrentProfile>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICatalogService>(sp => new CatalogService(
            sp.GetRequiredService<ApplicationDbContext>(),
            sp.GetRequiredService<ICurrentProfile>(),
            sp.GetRequiredService<ITmdbImporter>()));
        services.AddScoped<IBrowseService, BrowseService>();
        services.AddScoped<IMeService, MeService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IAdminService, AdminService>();

        // TMDB
        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.Section));
        services.AddTransient<TmdbThrottlingHandler>();
        services.AddHttpClient(TmdbClient.HttpClientName, c =>
            {
                c.BaseAddress = new Uri("https://api.themoviedb.org/3/");
                c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                c.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddHttpMessageHandler<TmdbThrottlingHandler>()
            .AddStandardResilienceHandler(o =>
            {
                // Retry exponencial com jitter em 429/5xx/timeout (respeita Retry-After)
                o.Retry.MaxRetryAttempts = 4;
                o.Retry.Delay = TimeSpan.FromSeconds(1);
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(55);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });
        services.AddSingleton<TmdbClient>();
        services.AddSingleton<ImportStatusTracker>();
        services.AddSingleton<ImportQueue>();
        services.AddSingleton<ITmdbImporter, TmdbImporter>();
        services.AddHostedService<ImportBackgroundService>();

        return services;
    }

    // ---------------------------------------------------------------- HTTP: CORS, cache, compressão, rate limit

    public const string CorsPolicy = "frontend";

    public static IServiceCollection AddHttpInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = (configuration["Cors:AllowedOrigins"] ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
        {
            if (origins.Contains("*"))
                p.AllowAnyOrigin();
            else
                p.WithOrigins(origins).SetIsOriginAllowedToAllowWildcardSubdomains().AllowCredentials();
            p.AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Profile-Id");
        }));

        services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
        });

        // Cache de saída só para requisições anônimas (a política padrão ignora requests com Authorization)
        services.AddOutputCache(o =>
        {
            o.AddPolicy(CachePolicies.Catalog, b => b
                .Expire(TimeSpan.FromSeconds(60))
                .SetVaryByQuery("*")
                .SetVaryByHeader(CurrentProfile.HeaderName)
                .Tag(CacheTags.Catalog));
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.ContentType = "application/json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Error(429, "Muitas requisições. Tente novamente em instantes."), ct);
            };

            // Por usuário autenticado (o front é um BFF, então IP sozinho agruparia todo mundo), senão por IP
            options.AddPolicy("fixed", http =>
            {
                var key = http.User.Identity?.IsAuthenticated == true
                    ? "u:" + http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    : "ip:" + (http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                               ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 300,
                    QueueLimit = 0
                });
            });

            options.AddPolicy("auth", http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "ip:" + (http.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 20, QueueLimit = 0 }));
        });

        return services;
    }

    // ---------------------------------------------------------------- Swagger

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MovieHub API",
                Version = "v2",
                Description = "Catálogo estilo streaming (filmes e séries) com perfis, Minha lista, progresso, reações e reviews."
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Cole apenas o access token (sem o prefixo Bearer)."
            });
            c.AddSecurityDefinition("Profile", new OpenApiSecurityScheme
            {
                Name = CurrentProfile.HeaderName,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Id (guid) do perfil ativo — obtido em GET /api/profiles."
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() },
                { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Profile" } }, Array.Empty<string>() }
            });

            var xml = Path.Combine(AppContext.BaseDirectory, "MovieHub.xml");
            if (File.Exists(xml)) c.IncludeXmlComments(xml);
            c.SupportNonNullableReferenceTypes();
            c.SchemaFilter<EnumLowercaseSchemaFilter>();
        });
        return services;
    }
}

/// <summary>Mostra enums no Swagger como strings minúsculas, igual ao JSON real.</summary>
public class EnumLowercaseSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!type.IsEnum) return;
        schema.Type = "string";
        schema.Format = null;
        schema.Enum = Enum.GetNames(type)
            .Select(n => (Microsoft.OpenApi.Any.IOpenApiAny)new Microsoft.OpenApi.Any.OpenApiString(JsonNamingPolicy.CamelCase.ConvertName(n)))
            .ToList();
    }
}

/// <summary>Mensagens do Identity em português.</summary>
public class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = $"O email '{email}' já está em uso." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"O email '{userName}' já está em uso." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = $"O email '{email}' é inválido." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"A senha deve ter pelo menos {length} caracteres." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "A senha deve conter pelo menos um número." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "A senha deve conter pelo menos uma letra minúscula." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "A senha deve conter pelo menos uma letra maiúscula." };
}
