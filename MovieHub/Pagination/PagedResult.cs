namespace MovieHub.Pagination;

public class PagedResult<T>
{
    // Lista de dados da página atual
    public IEnumerable<T> Data { get; set; } = new List<T>();

    // Página atual solicitada
    public int CurrentPage { get; set; }

    // Quantidade de registros por página
    public int PageSize { get; set; }

    // Total de registros disponíveis no banco 
    public int TotalCount { get; set; }

    // Total de páginas calculadas com base no TotalCount e PageSize
    public int TotalPages { get; set; }
}