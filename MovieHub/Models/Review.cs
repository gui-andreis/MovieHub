namespace MovieHub.Models;

// Representa uma avaliação feita por um usuário para um filme
public class Review
{
    // Chave primária da review
    public int Id { get; set; }

    // Nota atribuída ao filme (escala esperada: 1 a 5)
    public int Rating { get; set; } // 1 a 5

    // Comentário opcional do usuário
    public string? Comment { get; set; }

    // Data de criação da review (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Chave estrangeira para o filme avaliado
    public int MovieId { get; set; }

    // Navegação para o filme relacionado
    public Movie Movie { get; set; } = null!;

    // Chave estrangeira do usuário que criou a review
    public string UserId { get; set; } = string.Empty;

    // Navegação para o usuário autor da review
    public ApplicationUser User { get; set; } = null!;
}