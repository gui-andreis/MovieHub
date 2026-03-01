namespace MovieHub.Data;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieHub.Models;

// DbContext principal da aplicação
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    // DbSets, representam tabelas no banco
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Favorite> Favorites => Set<Favorite>();


    // Configuração de relacionamentos e chaves
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Necessário para configurar Identity corretamente

        // Chave composta para Favorite (UserId + MovieId)
        builder.Entity<Favorite>()
            .HasKey(f => new { f.UserId, f.MovieId });

        // Review -> Movie (1:N)
        builder.Entity<Review>()
            .HasOne(r => r.Movie)
            .WithMany(m => m.Reviews)
            .HasForeignKey(r => r.MovieId);

        // Review -> User (N:1)
        builder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId);

        // Favorite -> Movie (N:1)
        builder.Entity<Favorite>()
            .HasOne(f => f.Movie)
            .WithMany(m => m.Favorites)
            .HasForeignKey(f => f.MovieId);

        // Favorite -> User (N:1) 
        builder.Entity<Favorite>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId);
    }
}

// .OnDelete(DeleteBehavior.Restrict); melhoria futura