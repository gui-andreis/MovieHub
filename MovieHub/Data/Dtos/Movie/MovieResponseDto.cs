namespace MovieHub.Data.Dtos.Movie;

public class MovieResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public double AverageRating { get; set; }
    public string? ImagePath { get; set; }
    public List<string> Genres { get; set; } = new();
}