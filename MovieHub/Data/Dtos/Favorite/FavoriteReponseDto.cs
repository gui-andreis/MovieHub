namespace MovieHub.Data.Dtos.Favorite;

public class FavoriteReponseDto
{
    public int MovieId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public int ReleaseYear { get; set; }
}