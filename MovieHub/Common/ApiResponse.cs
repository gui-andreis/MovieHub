using System.Text.Json.Serialization;

namespace MovieHub.Common;

/// <summary>Envelope padrão de todas as respostas: { status, message, data }.</summary>
public class ApiResponse<T>
{
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; set; }

    public static ApiResponse<T> Success(T data, string message = "OK", int status = 200)
        => new() { Status = status, Message = message, Data = data };

    public static ApiResponse<object> NoContent(string message = "Operação realizada com sucesso.")
        => new() { Status = 200, Message = message, Data = null };

    public static ApiResponse<object> Error(int status, string message, IDictionary<string, string[]>? errors = null)
        => new() { Status = status, Message = message, Data = null, Errors = errors };
}

/// <summary>Resultado paginado: { items, page, pageSize, totalCount, totalPages }.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}
