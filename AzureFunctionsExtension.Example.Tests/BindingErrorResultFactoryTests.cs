namespace AzureFunctionsExtension.Example.Tests;

using System.Collections.Generic;

using AzureFunctionsExtension.Binders;
using AzureFunctionsExtension.Example.Binders;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

public sealed class BindingErrorResultFactoryTests
{
    [Fact]
    public async Task InvalidValueReturnsProblemDetails()
    {
        var services = HandlerTestHost.CreateServices(static x => x.AddSingleton<IBindingErrorResultFactory, BindingErrorResultFactory>());
        var req = HandlerTestHost.CreateRequest(
            services,
            query: new Dictionary<string, StringValues> { ["a"] = "notanumber" });

        var result = await HttpFunction.Query_Handler(req, HandlerTestHost.CreateContext(services));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
        Assert.Equal(["application/problem+json"], objectResult.ContentTypes);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal(["InvalidValue"], problem.Errors["a"]);
    }

    [Fact]
    public async Task ValidationFailureReturnsProblemDetailsWithMessages()
    {
        var services = HandlerTestHost.CreateServices(static x => x.AddSingleton<IBindingErrorResultFactory, BindingErrorResultFactory>());
        var req = HandlerTestHost.CreateRequest(services, body: HandlerTestHost.Json(new { id = 1, name = string.Empty }));

        var result = await HttpFunction.Body_Handler(req, HandlerTestHost.CreateContext(services));

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(["The Name field is required."], problem.Errors["request"]);
    }
}
