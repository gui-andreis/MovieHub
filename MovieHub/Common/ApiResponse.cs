namespace MovieHub.Common;

public class ApiResponse<T>
{
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data, string message = "OK", int status = 200)
        => new() { Status = status, Message = message, Data = data };

    public static ApiResponse<object> NoContent(string message = "Operação realizada com sucesso.")
        => new() { Status = 204, Message = message, Data = null };
}