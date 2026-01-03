using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace Koornik.API.Extensions;

public static class ErrorOrExtensions
{
    public static IActionResult ToActionResult<T>(this ErrorOr<T> result)
    {
        if (result.IsError)
        {
            return result.ToErrorActionResult();
        }

        return new OkObjectResult(result.Value);
    }

    public static IActionResult ToCreatedActionResult<T>(this ErrorOr<T> result, string routeName, Func<T, object> routeValuesFactory)
    {
        if (result.IsError)
        {
            return result.ToErrorActionResult();
        }

        return new CreatedAtRouteResult(routeName, routeValuesFactory(result.Value), result.Value);
    }

    public static IActionResult ToNoContentActionResult(this ErrorOr<Deleted> result)
    {
        if (result.IsError)
        {
            return result.ToErrorActionResult();
        }

        return new NoContentResult();
    }

    public static IActionResult ToNoContentActionResult(this ErrorOr<Success> result)
    {
        if (result.IsError)
        {
            return result.ToErrorActionResult();
        }

        return new NoContentResult();
    }

    private static IActionResult ToErrorActionResult<T>(this ErrorOr<T> result)
    {
        var firstError = result.FirstError;

        var statusCode = firstError.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var errors = result.Errors.Select(e => new { e.Code, e.Description }).ToList();

        return new ObjectResult(new { Errors = errors })
        {
            StatusCode = statusCode
        };
    }
}
