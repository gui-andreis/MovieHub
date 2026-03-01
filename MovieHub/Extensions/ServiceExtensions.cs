using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Services.Implementations;
using MovieHub.Services.Interfaces;

namespace MovieHub.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddAutoMapper(typeof(Program));

        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IFavoriteService, FavoriteService>();

        return services;
    }
}