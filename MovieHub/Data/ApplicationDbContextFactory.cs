using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MovieHub.Data;

// Usada pelo EF em design-time (dotnet ef migrations) para criar o DbContext fora do pipeline da aplicação
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5438;Database=movieHubDB;Username=postgres;Password=admin";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connection);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
