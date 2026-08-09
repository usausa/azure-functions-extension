namespace AzureFunctionsExtension.Mvc;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public class SystemTextJsonResult : ActionResult
{
    public SystemTextJsonResult(object value)
    {
        Value = value;
        StatusCode = StatusCodes.Status200OK;
    }

    public int? StatusCode { get; set; }

    public object? Value { get; set; }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Result serialization preserves the existing API shape.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Result serialization preserves the existing API shape.")]
    public override Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCode ?? StatusCodes.Status200OK;
        response.ContentType = "application/json";

        var options = context.HttpContext.RequestServices
            .GetService(typeof(IOptions<global::AzureFunctionsExtension.JsonOptions>)) is IOptions<global::AzureFunctionsExtension.JsonOptions> jsonOpts
            ? jsonOpts.Value.Options
            : null;

        // Serialization stops with the client. Note that the runtime type of Value is used:
        // when JsonOptions is configured with a source-generated JsonSerializerContext, that
        // runtime type must be registered in the context.
        return JsonSerializer.SerializeAsync(
            response.Body,
            Value,
            Value?.GetType() ?? typeof(object),
            options,
            context.HttpContext.RequestAborted);
    }
}
