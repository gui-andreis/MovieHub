namespace MovieHub.Data.Dtos.Favorite;

using System.ComponentModel.DataAnnotations;

public class AddFavoriteDto
{
    [Required]
    public int MovieId { get; set; }
}