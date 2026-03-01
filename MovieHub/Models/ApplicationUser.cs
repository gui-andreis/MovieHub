namespace MovieHub.Models;

using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    // Nome completo do usuário (campo adicional ao Identity padrão)
    public string? FullName { get; set; }
}