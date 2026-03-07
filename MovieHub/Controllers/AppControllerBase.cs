using Microsoft.AspNetCore.Mvc;
using MovieHub.Common;

namespace MovieHub.Controllers;

public abstract class AppControllerBase : ControllerBase
{
    protected IActionResult Ok<T>(T data, string message = "OK")
        => base.Ok(ApiResponse<T>.Success(data, message));

    protected IActionResult Created<T>(T data, string action, object routeValues, string message = "Criado com sucesso.")
        => base.CreatedAtAction(action, routeValues, ApiResponse<T>.Success(data, message, 201));

    protected IActionResult NoContent(string message = "Operação realizada com sucesso.")
        => base.Ok(ApiResponse<object>.NoContent(message));
}