namespace AzureFunctionsExtension.Example.Binders;

using AzureFunctionsExtension.Binders;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal sealed class BindingErrorResultFactory : IBindingErrorResultFactory
{
    public IActionResult Create(BindingError bindingError)
    {
        var problem = new ValidationProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Errors =
            {
                [bindingError.Name] = bindingError.ValidationResults.Count > 0
                    ? [.. bindingError.ValidationResults.Select(static x => x.ErrorMessage ?? string.Empty)]
                    : [bindingError.Kind.ToString()]
            }
        };

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }
}
