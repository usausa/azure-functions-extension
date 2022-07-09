namespace AzureFunctionsExtension.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

public class SystemTextJsonResult : ActionResult
{
    public SystemTextJsonResult(object value)
    {
        Value = value;
        StatusCode = StatusCodes.Status200OK;
    }

    public int? StatusCode { get; set; }

    public object? Value { get; set; }

    public override Task ExecuteResultAsync(ActionContext context)
    {
        var services = context.HttpContext.RequestServices;
        var executor = services.GetRequiredService<SystemTextJsonResultExecutor>();
        return executor.ExecuteAsync(context, this);
    }
}
