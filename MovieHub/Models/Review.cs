namespace MovieHub.Models;

public class Review
{
    public int Id { get; set; }

    public int Rating { get; set; } // 1 a 5

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Relacionamentos
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}