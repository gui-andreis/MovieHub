using System.Net;
using System.Text.Json;
using MovieHub.Exceptions;
namespace MovieHub.Middleware;

// Middleware responsável por capturar exceptions globais
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next; // Próximo middleware no pipeline
    private readonly ILogger<ExceptionMiddleware> _logger; // Logger para registrar erros

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Executado automaticamente a cada requisição HTTP
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);// Executa o próximo middleware ou controller
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado: {Message}", ex.Message); // Registra erro no log
            await HandleExceptionAsync(context, ex); // Trata e padroniza resposta de erro
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Define status HTTP baseado no tipo da exception
        var (statusCode, message) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            BadRequestException => (HttpStatusCode.BadRequest, exception.Message),
            ForbiddenException => (HttpStatusCode.Forbidden, exception.Message),
            UnauthorizedException => (HttpStatusCode.Unauthorized, exception.Message),
            ConflictException => (HttpStatusCode.Conflict, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro interno no servidor.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            status = (int)statusCode,
            message,
            data = (object?)null
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response)); // Retorna JSON padronizado
    }
}