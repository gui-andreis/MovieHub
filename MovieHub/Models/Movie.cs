namespace MovieHub.Models;

public class Movie
{
    public int Id { get; set; } // não precisa passar como 

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int ReleaseYear { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Relacionamentos
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}