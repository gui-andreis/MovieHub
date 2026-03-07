namespace MovieHub.Data.Dtos.Movie;

using System.ComponentModel.DataAnnotations;

public class CreateMovieDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1900, 2100)]
    public int ReleaseYear { get; set; }

    // Imagem opcional do filme
    public IFormFile? Image { get; set; }

    // IDs dos gêneros associados ao filme
    public List<int> GenreIds { get; set; } = new();
}
