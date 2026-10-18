namespace AzureFunctionsExtension.Tests;

using System.Text.Json;
using System.Text.RegularExpressions;

using AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static AzureFunctionsExtension.Tests.CompilationHelper;

public sealed class CatalogTests
{
    private const string AllTriggersSource =
        """
        namespace TestFunctions;

        using AzureFunctionsExtension.Annotations;
        using Microsoft.AspNetCore.Mvc;

        [AzureFunction]
        public sealed partial class SampleFunction
        {
            [HttpEndpoint("get", "http")]
            public IActionResult Http() => new EmptyResult();

            [TimerEndpoint("0 */5 * * * *")]
            public void Tick()
            {
            }

            [QueueEndpoint("orders")]
            public void Receive([FromTrigger] string message)
            {
            }
        }
        """;

    private const string HttpSource =
        """
        namespace TestFunctions;

        using System.Threading.Tasks;
        using AzureFunctionsExtension.Annotations;
        using Microsoft.AspNetCore.Mvc;

        [AzureFunction]
        public sealed partial class SampleFunction
        {
            [HttpEndpoint("get", "items/{id}")]
            public IActionResult GetItem([AzureFunctionsExtension.Annotations.FromRoute] int id) => new OkObjectResult(id);

            [HttpEndpoint("post", "items")]
            public async Task<IActionResult> Create()
            {
                await Task.Yield();
                return new EmptyResult();
            }
        }
        """;

    [Fact]
    public void GeneratesHttpMetadata()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Azure.Functions.Worker;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "items/{id}")]
                public IActionResult GetItem([AzureFunctionsExtension.Annotations.FromRoute] int id) => new OkObjectResult(id);

                [HttpEndpoint("post", "items", AuthorizationLevel.Anonymous)]
                public IActionResult Create() => new EmptyResult();
            }
            """;

        var bindings = GetBindings(RunCatalog(source));

        string[] expected =
        [
            """{"name":"req","type":"httpTrigger","direction":"In","authLevel":"Function","methods":["get"],"route":"items/{id}"}""",
            """{"name":"$return","type":"http","direction":"Out"}""",
            """{"name":"req","type":"httpTrigger","direction":"In","authLevel":"Anonymous","methods":["post"],"route":"items"}""",
            """{"name":"$return","type":"http","direction":"Out"}"""
        ];
        Assert.Equal(expected, bindings);
    }

    [Fact]
    public void GeneratesTimerMetadata()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [TimerEndpoint("0 */5 * * * *")]
                public void Tick()
                {
                }
            }
            """;

        var bindings = GetBindings(RunCatalog(source));

        string[] expected = ["""{"name":"timerInfo","type":"timerTrigger","direction":"In","schedule":"0 */5 * * * *"}"""];
        Assert.Equal(expected, bindings);
    }

    [Fact]
    public void GeneratesQueueMetadataWithAndWithoutConnection()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [QueueEndpoint("orders")]
                public void Receive([FromTrigger] string message)
                {
                }

                [QueueEndpoint("orders", "OrderConnection")]
                public void ReceiveWithConnection([FromTrigger] string message)
                {
                }
            }
            """;

        var bindings = GetBindings(RunCatalog(source));

        string[] expected =
        [
            """{"name":"message","type":"queueTrigger","direction":"In","queueName":"orders","dataType":"String"}""",
            """{"name":"message","type":"queueTrigger","direction":"In","queueName":"orders","connection":"OrderConnection","dataType":"String"}"""
        ];
        Assert.Equal(expected, bindings);
    }

    [Fact]
    public void EscapesBindingJson()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "a\"b\\c\td\u0001e")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var bindings = GetBindings(RunCatalog(source));

        using var document = JsonDocument.Parse(bindings[0]);
        Assert.Equal("a\"b\\c\td\u0001e", document.RootElement.GetProperty("route").GetString());
    }

    [Fact]
    public void BindingJsonIsParsableForAllTriggers()
    {
        var bindings = GetBindings(RunCatalog(AllTriggersSource));

        Assert.Equal(4, bindings.Length);
        foreach (var binding in bindings)
        {
            using var document = JsonDocument.Parse(binding);
            var root = document.RootElement;
            Assert.Equal(JsonValueKind.String, root.GetProperty("name").ValueKind);
            Assert.Equal(JsonValueKind.String, root.GetProperty("type").ValueKind);
            Assert.Equal(JsonValueKind.String, root.GetProperty("direction").ValueKind);
        }
    }

    [Fact]
    public void UsesAssemblyNameForScriptFile()
    {
        var catalog = GetCatalog(RunGenerator(HttpSource, "MyApp"));

        Assert.Contains("private const string ScriptFile = \"MyApp.dll\";", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesMetadataNameForEntryPoint()
    {
        const string source =
            """
            namespace @class;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class @event
            {
                [HttpEndpoint("get", "keyword")]
                public IActionResult @default() => new EmptyResult();
            }
            """;

        var catalog = RunCatalog(source);

        Assert.Contains("EntryPoint = \"class.event.default_Handler\",", catalog, StringComparison.Ordinal);
        Assert.Contains("case \"class.event.default_Handler\":", catalog, StringComparison.Ordinal);
        Assert.Contains("await global::@class.@event.default_Handler(", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesMetadataNameForEntryPointInGlobalNamespace()
    {
        const string source =
            """
            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class GlobalFunction
            {
                [HttpEndpoint("get", "global")]
                public IActionResult Run() => new EmptyResult();
            }
            """;

        var catalog = RunCatalog(source);

        Assert.Contains("EntryPoint = \"GlobalFunction.Run_Handler\",", catalog, StringComparison.Ordinal);
        Assert.Contains("await global::GlobalFunction.Run_Handler(", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratesDispatchForEachHandler()
    {
        var catalog = RunCatalog(AllTriggersSource);

        Assert.Matches(@"case ""TestFunctions\.SampleFunction\.Http_Handler"":\s+return __Execute0__\(context\);", catalog);
        Assert.Matches(@"case ""TestFunctions\.SampleFunction\.Tick_Handler"":\s+return __Execute1__\(context\);", catalog);
        Assert.Matches(@"case ""TestFunctions\.SampleFunction\.Receive_Handler"":\s+return __Execute2__\(context\);", catalog);
        Assert.Equal(1, CountOf(catalog, "(global::Microsoft.AspNetCore.Http.HttpRequest)values[0]!"));
        Assert.Equal(1, CountOf(catalog, "(global::Microsoft.Azure.Functions.Worker.TimerInfo)values[0]!"));
        Assert.Equal(1, CountOf(catalog, "((string)values[0]!"));
        Assert.Equal(1, CountOf(catalog, "GetInvocationResult(context).Value = await global::TestFunctions.SampleFunction.Http_Handler("));
        Assert.Equal(1, CountOf(catalog, "GetInvocationResult"));
    }

    [Fact]
    public void DoesNotGenerateCatalogWithoutHandlers()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;

            [AzureFunction]
            public sealed partial class EmptyFunction
            {
                public void Helper()
                {
                }
            }
            """;

        var result = RunGenerator(source);

        AssertNoGeneratorErrors(result);
        Assert.DoesNotContain(FunctionCatalogSourceBuilder.HintName, result.Sources);
    }

    [Fact]
    public void DoesNotIncludeClassWithError()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class ValidFunction
            {
                [HttpEndpoint("get", "valid")]
                public IActionResult Valid() => new EmptyResult();
            }

            [AzureFunction]
            public sealed class InvalidFunction
            {
                [HttpEndpoint("get", "invalid")]
                public IActionResult Invalid() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);
        var catalog = GetCatalog(result);

        Assert.Contains(result.Diagnostics, static x => x.Id == "AFE0001");
        Assert.Contains("EntryPoint = \"TestFunctions.ValidFunction.Valid_Handler\",", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidFunction", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotIncludeHintNameCollision()
    {
        const string source =
            """
            namespace TestFunctions;

            using AzureFunctionsExtension.Annotations;
            using Microsoft.AspNetCore.Mvc;

            [AzureFunction]
            public sealed partial class SampleFunction
            {
                [HttpEndpoint("get", "first")]
                public IActionResult First() => new EmptyResult();
            }

            [AzureFunction]
            public sealed partial class sampleFunction
            {
                [HttpEndpoint("get", "second")]
                public IActionResult Second() => new EmptyResult();
            }
            """;

        var result = RunGenerator(source);
        var catalog = GetCatalog(result);

        Assert.Contains(result.Diagnostics, static x => x.Id == "AFE0501");
        Assert.Contains("EntryPoint = \"TestFunctions.SampleFunction.First_Handler\",", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Second_Handler", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedCatalogCompiles()
    {
        Assert.Contains(FunctionCatalogSourceBuilder.HintName, RunGenerator(HttpSource).Sources);
        Assert.Empty(GetProblemIds(HttpSource));
    }

    private static string RunCatalog(string source) => GetCatalog(RunGenerator(source));

    private static string GetCatalog(GeneratorResult result) => result.Sources[FunctionCatalogSourceBuilder.HintName];

    private static string[] GetBindings(string catalog) =>
        CSharpSyntaxTree.ParseText(catalog).GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(static x => (x.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add", Expression: IdentifierNameSyntax target }) &&
                               target.Identifier.ValueText.StartsWith("bindings", StringComparison.Ordinal))
            .Select(static x => ((LiteralExpressionSyntax)x.ArgumentList.Arguments[0].Expression).Token.ValueText)
            .ToArray();

    private static int CountOf(string text, string value) => Regex.Count(text, Regex.Escape(value));
}
