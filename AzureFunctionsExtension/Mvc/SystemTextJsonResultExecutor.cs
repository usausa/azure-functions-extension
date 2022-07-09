namespace AzureFunctionsExtension.Mvc;

using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using AzureFunctionsExtension;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

public sealed class SystemTextJsonResultExecutor : IActionResultExecutor<SystemTextJsonResult>
{
    private static readonly string DefaultContentType = new MediaTypeHeaderValue("application/json")
    {
        Encoding = Encoding.UTF8
    }.ToString();

    private readonly JsonSerializerOptions options;

    public SystemTextJsonResultExecutor(IOptions<JsonOptions> options)
    {
        this.options = options.Value.Options;
    }

    public async Task ExecuteAsync(ActionContext context, SystemTextJsonResult result)
    {
        var response = context.HttpContext.Response;

        response.ContentType = DefaultContentType;

        if (result.StatusCode != null)
        {
            response.StatusCode = result.StatusCode.Value;
        }

        var value = result.Value;
        var objectType = value?.GetType() ?? typeof(object);
        var responseStream = response.Body;
        try
        {
            await JsonSerializer.SerializeAsync(responseStream, value, objectType, options, context.HttpContext.RequestAborted).ConfigureAwait(false);
            await responseStream.FlushAsync(context.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
    }
}
