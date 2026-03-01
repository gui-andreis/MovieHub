using MovieHub.Middleware;

namespace MovieHub.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseAppMiddlewares(this WebApplication app)
    {
        app.UseMiddleware<ExceptionMiddleware>();

        return app;
    }
}
