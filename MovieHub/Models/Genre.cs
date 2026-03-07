namespace MovieHub.Models;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Relacionamento N:N com Movie via MovieGenre
    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
}