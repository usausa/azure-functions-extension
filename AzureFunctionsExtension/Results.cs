namespace AzureFunctionsExtension;

using AzureFunctionsExtension.Mvc;

using Microsoft.AspNetCore.Mvc;

#pragma warning disable CA1054
public static class Results
{
    public static IActionResult Ok() => new OkResult();

    public static IActionResult Ok(object body) => new SystemTextJsonResult(body);

    public static IActionResult Created(string uri, object? body = null) => new CreatedResult(uri, body);

    public static IActionResult NoContent() => new NoContentResult();

    public static IActionResult BadRequest() => new BadRequestResult();

    public static IActionResult BadRequest(object body) => new BadRequestObjectResult(body);

    public static IActionResult NotFound() => new NotFoundResult();

    public static IActionResult NotFound(object body) => new NotFoundObjectResult(body);

    public static IActionResult Conflict() => new ConflictResult();

    public static IActionResult Conflict(object body) => new ConflictObjectResult(body);

    public static IActionResult StatusCode(int statusCode) => new StatusCodeResult(statusCode);

    public static IActionResult StatusCode(int statusCode, object body) => new ObjectResult(body) { StatusCode = statusCode };
}
#pragma warning restore CA1054
