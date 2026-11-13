using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieHub.Models;

namespace MovieHub.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Title> Titles => Set<Title>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<TitleGenre> TitleGenres => Set<TitleGenre>();
    public DbSet<Keyword> Keywords => Set<Keyword>();
    public DbSet<TitleKeyword> TitleKeywords => Set<TitleKeyword>();
    public DbSet<WatchProvider> WatchProviders => Set<WatchProvider>();
    public DbSet<TitleProvider> TitleProviders => Set<TitleProvider>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Credit> Credits => Set<Credit>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<MyListItem> MyListItems => Set<MyListItem>();
    public DbSet<WatchProgress> WatchProgress => Set<WatchProgress>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.Name).HasMaxLength(100);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(r => r.TokenHash).IsUnique();
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Title>(e =>
        {
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Name).HasMaxLength(300).IsRequired();
            e.Property(t => t.OriginalName).HasMaxLength(300);
            e.Property(t => t.Tagline).HasMaxLength(500);
            e.Property(t => t.AgeRating).HasMaxLength(4);
            e.Property(t => t.TrailerKey).HasMaxLength(32);
            e.Property(t => t.OriginalLanguage).HasMaxLength(8);
            e.Property(t => t.Status).HasMaxLength(50);
            e.Property(t => t.SearchText).HasMaxLength(700);
            e.HasIndex(t => new { t.TmdbId, t.Type }).IsUnique();
            e.HasIndex(t => t.Popularity);
            e.HasIndex(t => t.ReleaseDate);
            e.HasIndex(t => t.Type);
            e.HasIndex(t => t.SearchText);
            e.HasOne(t => t.Collection).WithMany(c => c.Titles).HasForeignKey(t => t.CollectionId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Genre>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(80).IsRequired();
            e.Property(g => g.Slug).HasMaxLength(80).IsRequired();
            e.HasIndex(g => g.Slug).IsUnique();
            e.HasIndex(g => g.TmdbId).IsUnique();
        });

        builder.Entity<TitleGenre>(e =>
        {
            e.HasKey(x => new { x.TitleId, x.GenreId });
            e.HasOne(x => x.Title).WithMany(t => t.TitleGenres).HasForeignKey(x => x.TitleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Genre).WithMany(g => g.TitleGenres).HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Keyword>(e =>
        {
            e.Property(k => k.Name).HasMaxLength(150);
            e.HasIndex(k => k.TmdbId).IsUnique();
        });

        builder.Entity<TitleKeyword>(e =>
        {
            e.HasKey(x => new { x.TitleId, x.KeywordId });
            e.HasOne(x => x.Title).WithMany(t => t.TitleKeywords).HasForeignKey(x => x.TitleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Keyword).WithMany(k => k.TitleKeywords).HasForeignKey(x => x.KeywordId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WatchProvider>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(120);
            e.HasIndex(p => p.TmdbId).IsUnique();
        });

        builder.Entity<TitleProvider>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            e.HasKey(x => new { x.TitleId, x.ProviderId, x.Kind });
            e.HasOne(x => x.Title).WithMany(t => t.TitleProviders).HasForeignKey(x => x.TitleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Provider).WithMany(p => p.TitleProviders).HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Collection>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(300);
            e.HasIndex(c => c.TmdbId).IsUnique();
        });

        builder.Entity<Person>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.SearchName).HasMaxLength(200);
            e.Property(p => p.PlaceOfBirth).HasMaxLength(300);
            e.Property(p => p.KnownForDepartment).HasMaxLength(60);
            e.HasIndex(p => p.TmdbId).IsUnique();
            e.HasIndex(p => p.SearchName);
        });

        builder.Entity<Credit>(e =>
        {
            e.Property(c => c.Role).HasConversion<string>().HasMaxLength(10);
            e.Property(c => c.Character).HasMaxLength(300);
            e.Property(c => c.Job).HasMaxLength(80);
            e.HasIndex(c => new { c.TitleId, c.Role, c.Order });
            e.HasOne(c => c.Title).WithMany(t => t.Credits).HasForeignKey(c => c.TitleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Person).WithMany(p => p.Credits).HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Season>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(200);
            e.HasIndex(s => new { s.TitleId, s.SeasonNumber }).IsUnique();
            e.HasOne(s => s.Title).WithMany(t => t.Seasons).HasForeignKey(s => s.TitleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Episode>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(300);
            e.HasIndex(x => new { x.SeasonId, x.EpisodeNumber }).IsUnique();
            e.HasOne(x => x.Season).WithMany(s => s.Episodes).HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Profile>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(50).IsRequired();
            e.Property(p => p.AvatarKey).HasMaxLength(40);
            e.Property(p => p.MaturityLimit).HasMaxLength(4);
            e.Property(p => p.Language).HasMaxLength(10);
            e.HasOne(p => p.User).WithMany(u => u.Profiles).HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MyListItem>(e =>
        {
            e.HasKey(x => new { x.ProfileId, x.TitleId });
            e.HasOne(x => x.Profile).WithMany(p => p.MyList).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Title).WithMany().HasForeignKey(x => x.TitleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WatchProgress>(e =>
        {
            e.HasIndex(x => new { x.ProfileId, x.TitleId, x.EpisodeId });
            e.HasIndex(x => new { x.ProfileId, x.UpdatedAt });
            e.HasOne(x => x.Profile).WithMany(p => p.Progress).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Title).WithMany().HasForeignKey(x => x.TitleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Episode).WithMany().HasForeignKey(x => x.EpisodeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Reaction>(e =>
        {
            e.Property(x => x.Value).HasConversion<string>().HasMaxLength(10);
            e.HasKey(x => new { x.ProfileId, x.TitleId });
            e.HasOne(x => x.Profile).WithMany(p => p.Reactions).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Title).WithMany().HasForeignKey(x => x.TitleId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Review>(e =>
        {
            e.Property(r => r.Comment).HasMaxLength(4000);
            e.HasIndex(r => new { r.ProfileId, r.TitleId }).IsUnique();
            e.HasIndex(r => new { r.TitleId, r.CreatedAt });
            e.HasOne(r => r.Profile).WithMany(p => p.Reviews).HasForeignKey(r => r.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Title).WithMany(t => t.Reviews).HasForeignKey(r => r.TitleId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
