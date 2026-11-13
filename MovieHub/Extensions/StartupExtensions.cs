using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Models;

namespace MovieHub.Extensions;

public static class StartupExtensions
{
    /// <summary>Aplica migrations pendentes (com retry — o Postgres do Neon pode estar "acordando").</summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Banco de dados atualizado (migrations aplicadas).");
                return;
            }
            catch (Exception ex) when (attempt < 5)
            {
                logger.LogWarning(ex, "Falha ao aplicar migrations (tentativa {Attempt}/5). Tentando novamente…", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 3));
            }
        }
    }

    /// <summary>Garante as roles padrão e o usuário Admin inicial (Admin__Email / Admin__Password).</summary>
    public static async Task SeedRolesAndAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await userManager.FindByEmailAsync(email) != null) return;

        var admin = new ApplicationUser { UserName = email, Email = email, Name = "Administrador", EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Não foi possível criar o admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, new[] { "Admin", "User" });
        db.Profiles.Add(new Profile { UserId = admin.Id, Name = "Admin", AvatarKey = "avatar-12" });
        await db.SaveChangesAsync();
        logger.LogInformation("Usuário admin {Email} criado.", email);
    }
}
