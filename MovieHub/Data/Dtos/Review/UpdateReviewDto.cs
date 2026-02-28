namespace MovieHub.Data.Dtos.Review;

using System.ComponentModel.DataAnnotations;

public class UpdateReviewDto
{
    [Required]
    public int Id { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }
}