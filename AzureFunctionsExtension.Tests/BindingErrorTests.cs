namespace AzureFunctionsExtension.Tests;

using System.ComponentModel.DataAnnotations;
using System.Reflection;

using AzureFunctionsExtension.Binders;
using AzureFunctionsExtension.Validation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

public sealed class BindingErrorTests
{
    private const string Head =
        """
        namespace Test;

        using System.Threading.Tasks;
        using AzureFunctionsExtension.Annotations;
        using AzureFunctionsExtension.Filters;
        using Microsoft.AspNetCore.Mvc;

        public sealed class Item
        {
            public string? Name { get; set; }
        }

        public sealed class PassFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next) => next(context);
        }

        """;

    private static string Function(string filter) =>
        $$"""
        [AzureFunction]
        {{filter}}
        public sealed partial class Function
        {
            [HttpEndpoint("get", "query")]
            public IActionResult Query([AzureFunctionsExtension.Annotations.FromQuery] int page) => new OkResult();

            [HttpEndpoint("get", "query-nullable")]
            public IActionResult QueryNullable([AzureFunctionsExtension.Annotations.FromQuery] int? page) => new OkResult();

            [HttpEndpoint("get", "query-array")]
            public IActionResult QueryArray([AzureFunctionsExtension.Annotations.FromQuery] int[] ids) => new OkResult();

            [HttpEndpoint("get", "route/{id}")]
            public IActionResult Route([AzureFunctionsExtension.Annotations.FromRoute] int id) => new OkResult();

            [HttpEndpoint("get", "route-nullable/{id}")]
            public IActionResult RouteNullable([AzureFunctionsExtension.Annotations.FromRoute] int? id) => new OkResult();

            [HttpEndpoint("get", "route-array/{ids}")]
            public IActionResult RouteArray([AzureFunctionsExtension.Annotations.FromRoute] int?[] ids) => new OkResult();

            [HttpEndpoint("get", "header")]
            public IActionResult Header([AzureFunctionsExtension.Annotations.FromHeader("x-count")] int count) => new OkResult();

            [HttpEndpoint("get", "header-nullable")]
            public IActionResult HeaderNullable([AzureFunctionsExtension.Annotations.FromHeader("x-count")] int? count) => new OkResult();

            [HttpEndpoint("get", "header-array")]
            public IActionResult HeaderArray([AzureFunctionsExtension.Annotations.FromHeader("x-ids")] int[] ids) => new OkResult();

            [HttpEndpoint("get", "cookie")]
            public IActionResult Cookie([AzureFunctionsExtension.Annotations.FromCookie("count")] int count) => new OkResult();

            [HttpEndpoint("get", "cookie-nullable")]
            public IActionResult CookieNullable([AzureFunctionsExtension.Annotations.FromCookie("count")] int? count) => new OkResult();

            [HttpEndpoint("post", "body")]
            public IActionResult Body([AzureFunctionsExtension.Annotations.FromBody] Item item) => new OkResult();

            [HttpEndpoint("get", "poco")]
            public string Poco([AzureFunctionsExtension.Annotations.FromQuery] int page) => "ok";
        }
        """;

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task InvalidValueIsPassedToFactory(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + Function(filter));
        var services = CreateServices(registerFactory: true);

        await AssertFactoryResult(assembly, services, "Query_Handler", HandlerTestHost.CreateRequest(services, query: Values("page", "x")), "InvalidValue:Query:page:0");
        await AssertFactoryResult(assembly, services, "QueryNullable_Handler", HandlerTestHost.CreateRequest(services, query: Values("page", "x")), "InvalidValue:Query:page:0");
        await AssertFactoryResult(assembly, services, "QueryArray_Handler", HandlerTestHost.CreateRequest(services, query: Values("ids", "1,x")), "InvalidValue:Query:ids:0");
        await AssertFactoryResult(assembly, services, "Route_Handler", HandlerTestHost.CreateRequest(services, route: RouteValues("id", "x")), "InvalidValue:Route:id:0");
        await AssertFactoryResult(assembly, services, "RouteNullable_Handler", HandlerTestHost.CreateRequest(services, route: RouteValues("id", "x")), "InvalidValue:Route:id:0");
        await AssertFactoryResult(assembly, services, "RouteArray_Handler", HandlerTestHost.CreateRequest(services, route: RouteValues("ids", "1,x")), "InvalidValue:Route:ids:0");
        await AssertFactoryResult(assembly, services, "Header_Handler", HandlerTestHost.CreateRequest(services, headers: Values("x-count", "x")), "InvalidValue:Header:x-count:0");
        await AssertFactoryResult(assembly, services, "HeaderNullable_Handler", HandlerTestHost.CreateRequest(services, headers: Values("x-count", "x")), "InvalidValue:Header:x-count:0");
        await AssertFactoryResult(assembly, services, "HeaderArray_Handler", HandlerTestHost.CreateRequest(services, headers: Values("x-ids", "1,x")), "InvalidValue:Header:x-ids:0");
        await AssertFactoryResult(assembly, services, "Cookie_Handler", HandlerTestHost.CreateRequest(services, headers: Values("Cookie", "count=x")), "InvalidValue:Cookie:count:0");
        await AssertFactoryResult(assembly, services, "CookieNullable_Handler", HandlerTestHost.CreateRequest(services, headers: Values("Cookie", "count=x")), "InvalidValue:Cookie:count:0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task BodyErrorsArePassedToFactory(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + Function(filter));
        var services = CreateServices(registerFactory: true);

        await AssertFactoryResult(assembly, services, "Body_Handler", HandlerTestHost.CreateRequest(services, body: "{"), "InvalidBody:Body:item:0");
        await AssertFactoryResult(assembly, services, "Body_Handler", HandlerTestHost.CreateRequest(services, body: "null"), "MissingBody:Body:item:0");
        await AssertFactoryResult(assembly, services, "Body_Handler", HandlerTestHost.CreateRequest(services, body: "{}"), "ValidationFailed:Body:item:1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public Task PocoHandlerUsesFactory(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + Function(filter));
        var services = CreateServices(registerFactory: true);

        return AssertFactoryResult(assembly, services, "Poco_Handler", HandlerTestHost.CreateRequest(services, query: Values("page", "x")), "InvalidValue:Query:page:0");
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task WithoutFactoryReturnsTextBadRequest(string filter)
    {
        var assembly = CompilationHelper.LoadAssembly(Head + Function(filter));
        var services = CreateServices(registerFactory: false);

        var query = await Invoke(assembly, services, "Query_Handler", HandlerTestHost.CreateRequest(services, query: Values("page", "x")));
        Assert.Equal(400, HandlerTestHost.StatusOf(query));
        Assert.Equal("Invalid parameter: page", HandlerTestHost.ValueOf<string>(query));

        var poco = await Invoke(assembly, services, "Poco_Handler", HandlerTestHost.CreateRequest(services, query: Values("page", "x")));
        Assert.Equal(400, HandlerTestHost.StatusOf(poco));
        Assert.Equal("Invalid parameter: page", HandlerTestHost.ValueOf<string>(poco));

        var body = await Invoke(assembly, services, "Body_Handler", HandlerTestHost.CreateRequest(services, body: "{}"));
        Assert.Equal(400, HandlerTestHost.StatusOf(body));
        Assert.StartsWith("Validation failed: item", HandlerTestHost.ValueOf<string>(body), StringComparison.Ordinal);
    }

    [Fact]
    public void HelperIsNotGeneratedWithoutHttpFunction()
    {
        var result = CompilationHelper.RunGenerator(
            """
            namespace Test;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class Function
            {
                [QueueEndpoint("items")]
                public void Receive([FromTrigger] string message)
                {
                }
            }
            """);
        CompilationHelper.AssertNoGeneratorErrors(result);

        Assert.DoesNotContain(result.Sources.Values, static s => s.Contains("__BindingErrorResult__", StringComparison.Ordinal));
    }

    private static IServiceProvider CreateServices(bool registerFactory) =>
        HandlerTestHost.CreateServices(services =>
        {
            services.AddSingleton<IRequestValidator>(new FailingValidator());
            if (registerFactory)
            {
                services.AddSingleton<IBindingErrorResultFactory>(new TestErrorFactory());
            }
        });

    private static Dictionary<string, StringValues> Values(string key, string value) => new() { [key] = value };

    private static Dictionary<string, object?> RouteValues(string key, string value) => new() { [key] = value };

    private static Task<IActionResult> Invoke(Assembly assembly, IServiceProvider services, string handler, HttpRequest request) =>
        CompilationHelper.InvokeAsync(assembly, handler, request, HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken));

    private static async Task AssertFactoryResult(Assembly assembly, IServiceProvider services, string handler, HttpRequest request, string expected)
    {
        var result = await Invoke(assembly, services, handler, request);

        Assert.Equal(422, HandlerTestHost.StatusOf(result));
        Assert.Equal(expected, HandlerTestHost.ValueOf<string>(result));
    }

    private sealed class FailingValidator : IRequestValidator
    {
        public bool Validate(object value) => false;

        public bool Validate(object value, ICollection<ValidationResult> results)
        {
            results.Add(new ValidationResult("Name is required.", ["/name"]));
            return false;
        }
    }

    private sealed class TestErrorFactory : IBindingErrorResultFactory
    {
        public IActionResult Create(BindingError bindingError) =>
            new ObjectResult($"{bindingError.Kind}:{bindingError.Source}:{bindingError.Name}:{bindingError.ValidationResults.Count}") { StatusCode = 422 };
    }
}
