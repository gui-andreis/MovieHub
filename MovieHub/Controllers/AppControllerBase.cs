using Microsoft.AspNetCore.Mvc;
using MovieHub.Common;

namespace MovieHub.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class AppControllerBase : ControllerBase
{
    protected IActionResult Ok<T>(T data, string message = "OK")
        => base.Ok(ApiResponse<T>.Success(data, message));

    protected IActionResult Created<T>(T data, string message = "Criado com sucesso.")
        => StatusCode(201, ApiResponse<T>.Success(data, message, 201));

    protected IActionResult Accepted<T>(T data, string message)
        => StatusCode(202, ApiResponse<T>.Success(data, message, 202));

    protected IActionResult Done(string message = "Operação realizada com sucesso.")
        => base.Ok(ApiResponse<object>.NoContent(message));
}
