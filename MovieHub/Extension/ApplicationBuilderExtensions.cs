using MovieHub.Middleware;

namespace MovieHub.Extension;

// Classe estática pra poder usar como extensão
public static class ApplicationBuilderExtensions
{
    // "this WebApplication app" é o que faz virar um método de extensão
    // ou seja, você chama app.UseAppMiddlewares() em vez de ApplicationBuilderExtensions.UseAppMiddlewares(app)
    public static WebApplication UseAppMiddlewares(this WebApplication app)
    {
        app.UseMiddleware<ExceptionMiddleware>();

        // Quando adicionar novos middlewares no futuro, vem aqui
        // app.UseMiddleware<LoggingMiddleware>();
        // app.UseMiddleware<RateLimitMiddleware>();

        return app;
    }
}
