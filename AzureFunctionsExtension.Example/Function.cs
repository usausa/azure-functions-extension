namespace AzureFunctionsExtension.Example;

using AzureFunctionsExtension.Annotations;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

public class Function
{
    private readonly ILogger<Function> log;

    public Function(ILogger<Function> log)
    {
        this.log = log;
    }

    [FunctionName("Function")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060", Justification = "Ignore")]
    public IActionResult Query(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "query")] HttpRequest req,
        [BindQuery] int a,
        [BindQuery] int? b,
        [BindQuery] int c = 3)
    {
        log.LogInformation("Query request. a=[{A}], b=[{B}], c=[{C}]", a, b, c);

        return Results.Of(new QueryResponse
        {
            Result = a + (b ?? 0) + c
        });
    }
}
