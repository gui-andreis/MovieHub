namespace MovieHub.Models;

// Representa um filme cadastrado na aplicação
public class Movie
{
    // Chave primária (auto incremento no banco)
    public int Id { get; set; }

    // Título do filme
    public string Title { get; set; } = string.Empty;

    // Descrição/resumo do filme
    public string Description { get; set; } = string.Empty;

    // Ano de lançamento
    public int ReleaseYear { get; set; }

    // Data de criação do registro no sistema (UTC)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Relacionamento 1:N com Reviews
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    // Relacionamento N:N com usuários via entidade Favorite
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}