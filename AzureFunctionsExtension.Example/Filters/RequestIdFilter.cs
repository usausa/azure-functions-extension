namespace AzureFunctionsExtension.Example.Filters;

using AzureFunctionsExtension.Filters;

using Microsoft.AspNetCore.Http;

internal sealed class RequestIdFilter : IFunctionFilter
{
    public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
    {
        if (context.Request is not HttpRequest request)
        {
            return next(context);
        }

        request.HttpContext.Response.Headers["X-Request-Id"] = context.FunctionContext.InvocationId;
        return next(context);
    }
}
