namespace MovieHub.Data.Dtos.Review;

public class ReviewResponseDto
{
    public int Id { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MovieId { get; set; }
}