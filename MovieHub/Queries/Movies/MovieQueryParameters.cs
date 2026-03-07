namespace MovieHub.Queries.Movies;

public class MovieQueryParameters
{
    private const int MaxPageSize = 50;

    public int PageNumber { get; set; } = 1;

    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    // Filtros
    public string? Title { get; set; }
    public int? ReleaseYear { get; set; }
    public List<int>? GenreIds { get; set; }
    public double? MinRating { get; set; }
    public double? MaxRating { get; set; }

    // Ordenação: "title", "year", "rating"
    public string? OrderBy { get; set; }
}