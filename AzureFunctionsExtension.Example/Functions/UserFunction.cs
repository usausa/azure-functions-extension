namespace AzureFunctionsExtension.Example.Functions;

using System.Security.Claims;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Annotations;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;

[AzureFunction]
internal sealed partial class UserFunction
{
    private static readonly Action<ILogger, bool, Exception?> MeRequest =
        LoggerMessage.Define<bool>(LogLevel.Information, new EventId(1, nameof(Me)), "Me request. authenticated=[{Authenticated}]");

    private readonly ILogger<UserFunction> log;

    public UserFunction(ILogger<UserFunction> log)
    {
        this.log = log;
    }

    [HttpEndpoint("get", "me", AuthorizationLevel.Anonymous)]
    public IActionResult Me(ClaimsPrincipal user, [FromCookie("theme")] string? theme)
    {
        var authenticated = user.Identity?.IsAuthenticated == true;
        MeRequest(log, authenticated, null);
        return authenticated
            ? Results.Ok(new { Id = user.FindFirst("sub")?.Value, Admin = user.IsInRole("admin"), Theme = theme })
            : Results.StatusCode(401);
    }
}
