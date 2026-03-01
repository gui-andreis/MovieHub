namespace MovieHub.Data.Dtos.Favorite;

public class FavoriteResponseDto
{
    public int MovieId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public int ReleaseYear { get; set; }
}