using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Services.Implementations;
using MovieHub.Services.Interfaces;
using StackExchange.Redis;
namespace MovieHub.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        var redisConnection = configuration.GetConnectionString("Redis")!;
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();

        services.AddAutoMapper(typeof(Program));

        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<IGenreService, GenreService>();

        return services;
    }
}