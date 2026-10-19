namespace AzureFunctionsExtension.Tests;

using System.Reflection;

using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;

public sealed class AssemblyFilterTests
{
    private const string Head =
        """
        namespace Test;

        using System.Collections.Generic;
        using System.Threading.Tasks;
        using AzureFunctionsExtension.Annotations;
        using AzureFunctionsExtension.Filters;
        using Microsoft.AspNetCore.Mvc;

        public static class Trace
        {
            public static List<string> Items { get; } = [];

            public static string Take()
            {
                var value = string.Join(",", Items);
                Items.Clear();
                return value;
            }
        }

        public sealed class AFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
            {
                Trace.Items.Add("A");
                return next(context);
            }
        }

        public sealed class BFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
            {
                Trace.Items.Add("B");
                return next(context);
            }
        }

        """;

    private const string InvalidFilterSource =
        """
        namespace Test;

        using AzureFunctionsExtension.Annotations;
        using Microsoft.AspNetCore.Mvc;

        public sealed class NotAFilter
        {
        }

        [AzureFunction]
        public sealed partial class Function
        {
            [HttpEndpoint("get", "a")]
            public IActionResult Get() => new OkResult();
        }

        [AzureFunction]
        public sealed partial class OtherFunction
        {
            [HttpEndpoint("get", "b")]
            public IActionResult GetOther() => new OkResult();
        }
        """;

    private static string Functions(string classFilter) =>
        $$"""
        [AzureFunction]
        {{classFilter}}
        public sealed partial class Function
        {
            [HttpEndpoint("get", "a")]
            public IActionResult Get() => new OkObjectResult(Trace.Take());
        }

        [AzureFunction]
        public sealed partial class OtherFunction
        {
            [HttpEndpoint("get", "b")]
            public IActionResult GetOther() => new OkObjectResult(Trace.Take());
        }
        """;

    [Fact]
    public async Task AssemblyFilterAppliesToEveryClass()
    {
        var assembly = CompilationHelper.LoadAssembly("[assembly: AzureFunctionsExtension.Annotations.Filter<Test.AFilter>]" + Environment.NewLine + Head + Functions(string.Empty));

        Assert.Equal("A", await InvokeAsync(assembly, "Test.Function", "Get_Handler"));
        Assert.Equal("A", await InvokeAsync(assembly, "Test.OtherFunction", "GetOther_Handler"));
    }

    [Fact]
    public async Task AssemblyFilterIsOuterWithSameOrder()
    {
        var assembly = CompilationHelper.LoadAssembly("[assembly: AzureFunctionsExtension.Annotations.Filter<Test.AFilter>]" + Environment.NewLine + Head + Functions("[Filter<BFilter>]"));

        Assert.Equal("A,B", await InvokeAsync(assembly, "Test.Function", "Get_Handler"));
        Assert.Equal("A", await InvokeAsync(assembly, "Test.OtherFunction", "GetOther_Handler"));
    }

    [Fact]
    public async Task OrderIsSharedBetweenAssemblyAndClass()
    {
        var assembly = CompilationHelper.LoadAssembly("[assembly: AzureFunctionsExtension.Annotations.Filter<Test.AFilter>(Order = 10)]" + Environment.NewLine + Head + Functions("[Filter<BFilter>(Order = 0)]"));

        Assert.Equal("B,A", await InvokeAsync(assembly, "Test.Function", "Get_Handler"));
    }

    [Fact]
    public void AssemblyFilterDiagnosticIsReportedOnce()
    {
        var diagnostics = CompilationHelper.RunGenerator("[assembly: AzureFunctionsExtension.Annotations.Filter<Test.NotAFilter>]" + Environment.NewLine + InvalidFilterSource).Diagnostics;

        var diagnostic = Assert.Single(diagnostics, static x => x.Id == "AFE0201");
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal(0, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public void AssemblyFilterDiagnosticInOtherFileIsReportedOnce()
    {
        var (driver, compilation) = CompilationHelper.CreateDriver(InvalidFilterSource);
        var tree = compilation.SyntaxTrees.Single();
        compilation = compilation
            .ReplaceSyntaxTree(tree, tree.WithFilePath("Functions.cs"))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                "[assembly: AzureFunctionsExtension.Annotations.Filter<Test.NotAFilter>]",
                (CSharpParseOptions)tree.Options,
                "AssemblyInfo.cs",
                cancellationToken: TestContext.Current.CancellationToken));

        var diagnostics = driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult().Diagnostics;

        var diagnostic = Assert.Single(diagnostics, static x => x.Id == "AFE0201");
        Assert.Equal("AssemblyInfo.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.Equal(0, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    private static async Task<string?> InvokeAsync(Assembly assembly, string typeName, string handler)
    {
        var services = HandlerTestHost.CreateServices();
        var method = assembly.GetType(typeName, throwOnError: true)!.GetMethod(handler)!;
        var result = await (Task<IActionResult>)method.Invoke(null, [HandlerTestHost.CreateRequest(services), HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken)])!;
        return HandlerTestHost.ValueOf<string>(result);
    }
}
