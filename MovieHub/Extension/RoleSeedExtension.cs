using Microsoft.AspNetCore.Identity;
using MovieHub.Models;

namespace MovieHub.Extension;

public static class RoleSeedExtension
{
    // Cria as roles e o primeiro admin automaticamente quando a app sobe
    public static async Task SeedRolesAndAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Cria as roles se não existirem
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Cria o admin padrão se não existir
        // As credenciais vêm do appsettings.json
        var adminEmail = config["AdminSettings:Email"];
        var adminPassword = config["AdminSettings:Password"];

        if (adminEmail != null && adminPassword != null)
        {
            var adminExists = await userManager.FindByEmailAsync(adminEmail);
            if (adminExists == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Administrador"
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}