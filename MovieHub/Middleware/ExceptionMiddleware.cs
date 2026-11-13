using System.Net;
using System.Text.Json;
using MovieHub.Common;
using MovieHub.Exceptions;

namespace MovieHub.Middleware;

/// <summary>Converte exceções em respostas no envelope padrão { status, message, data }.</summary>
public class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente desistiu da requisição — nada a responder
        }
        catch (Exception ex)
        {
            var (status, message) = ex switch
            {
                NotFoundException => (HttpStatusCode.NotFound, ex.Message),
                BadRequestException => (HttpStatusCode.BadRequest, ex.Message),
                ForbiddenException => (HttpStatusCode.Forbidden, ex.Message),
                UnauthorizedException => (HttpStatusCode.Unauthorized, ex.Message),
                ConflictException => (HttpStatusCode.Conflict, ex.Message),
                _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro interno no servidor.")
            };

            if (status == HttpStatusCode.InternalServerError)
                _logger.LogError(ex, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                _logger.LogInformation("{Status} em {Method} {Path}: {Message}", (int)status, context.Request.Method, context.Request.Path, ex.Message);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse<object>.Error((int)status, message), Json));
        }
    }
}
