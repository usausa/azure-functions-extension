namespace AzureFunctionsExtension.Filters;

using System.Security.Claims;
using System.Threading;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

public sealed class FunctionInvocationContext
{
    public object? Request { get; init; }

    public FunctionContext FunctionContext { get; init; } = default!;

    public CancellationToken CancellationToken { get; init; }

    public object? Result { get; set; }

    public ClaimsPrincipal User
    {
        get => Request is HttpRequest request ? request.HttpContext.User : field ??= new ClaimsPrincipal(new ClaimsIdentity());
        set
        {
            if (Request is HttpRequest request)
            {
                request.HttpContext.User = value;
            }
            else
            {
                field = value;
            }
        }
    }

    private Dictionary<string, object?>? items;

    public IDictionary<string, object?> Items => items ??= [];
}
