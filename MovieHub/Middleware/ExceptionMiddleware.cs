using System.Net;
using System.Text.Json;

namespace MovieHub.Middleware;

// Essa classe É o middleware
// Ela precisa ter exatamente esse formato pro ASP.NET reconhecer
public class ExceptionMiddleware
{
    // RequestDelegate representa o "próximo passo" no pipeline
    // ou seja, o próximo middleware ou o controller
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // InvokeAsync é chamado automaticamente pelo ASP.NET em toda requisição
    // HttpContext tem tudo sobre a requisição atual (headers, body, user, etc)
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Chama o próximo passo no pipeline (controller, outro middleware, etc)
            // Se nenhuma exception for lançada, a requisição segue normal
            await _next(context);
        }
        catch (Exception ex)
        {
            // Se qualquer exception for lançada em QUALQUER lugar da aplicação
            // ela cai aqui — o middleware captura antes de chegar no usuário
            _logger.LogError(ex, "Erro não tratado: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Define o status HTTP e a mensagem baseado no tipo da exception
        var (statusCode, message) = exception switch
        {
            // Exception que você já usa no ReviewService e FavoriteService
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, exception.Message),

            // Exception genérica que você usa pra "Filme não encontrado", etc
            Exception e when e.Message.Contains("não encontrado")
                => (HttpStatusCode.NotFound, exception.Message),

            // Qualquer outra exception vira 500
            _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro interno no servidor.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        // Resposta padronizada em JSON
        var response = new
        {
            status = (int)statusCode,
            message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}