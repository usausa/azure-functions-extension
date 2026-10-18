namespace AzureFunctionsExtension.Example.Middleware;

using System.Security.Claims;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

internal sealed class ClientPrincipalMiddleware : IFunctionsWorkerMiddleware
{
    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if ((httpContext is not null) && httpContext.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var header))
        {
            httpContext.User = Parse(header.ToString());
        }

        return next(context);
    }

    public static ClaimsPrincipal Parse(string header)
    {
        using var document = JsonDocument.Parse(Convert.FromBase64String(header));
        var root = document.RootElement;
        var identity = new ClaimsIdentity(GetString(root, "auth_typ"), GetString(root, "name_typ"), GetString(root, "role_typ"));
        if (root.TryGetProperty("claims", out var claims))
        {
            foreach (var claim in claims.EnumerateArray())
            {
                identity.AddClaim(new Claim(GetString(claim, "typ")!, GetString(claim, "val")!));
            }
        }

        return new ClaimsPrincipal(identity);
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
