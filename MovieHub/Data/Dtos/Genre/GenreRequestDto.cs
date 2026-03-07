using System.ComponentModel.DataAnnotations;

namespace MovieHub.Data.Dtos.Genre;

public class GenreRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}