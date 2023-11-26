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

    [FunctionName("Query")]
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

    [FunctionName("Array")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060", Justification = "Ignore")]
    public IActionResult Array(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "array")] HttpRequest req,
        [BindQuery] int[] a,
        [BindQuery] int?[] b)
    {
        log.LogInformation("Query array request. a.Length=[{A}], b.Length=[{B}]", a.Length, b.Length);

        return Results.Of(new QueryResponse
        {
            Result = a.Sum() + b.Sum(static x => x ?? 0)
        });
    }

    [FunctionName("Body")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060", Justification = "Ignore")]
    public IActionResult Body(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "body")] HttpRequest req,
        [BindBody] BodyRequest request)
    {
        if (!ValidationHelper.Validate(request))
        {
            return new BadRequestResult();
        }

        log.LogInformation("Body request. id=[{Id}], name=[{Name}], flag=[{Flag}], dateTime=[{DateTime:yyyy/MM/dd HH:mm:ss}]", request.Id, request.Name, request.Flag, request.DateTime);

        return Results.Of(new BodyResponse
        {
            Id = request.Id,
            Name = request.Name,
            Flag = request.Flag,
            DateTime = DateTime.Now
        });
    }
}
