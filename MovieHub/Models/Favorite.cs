namespace MovieHub.Models;

// Representa a relação de favoritos entre usuário e filme 
public class Favorite
{
    // Chave estrangeira do usuário
    public string UserId { get; set; } = string.Empty;

    // Navegação para o usuário relacionado
    public ApplicationUser User { get; set; } = null!;

    // Chave estrangeira do filme
    public int MovieId { get; set; }

    // Navegação para o filme relacionado
    public Movie Movie { get; set; } = null!;
}