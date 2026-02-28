namespace MovieHub.Data.Dtos.Review;

using System.ComponentModel.DataAnnotations;

public class CreateReviewDto
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    [Required]
    public int MovieId { get; set; }
}