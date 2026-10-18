namespace AzureFunctionsExtension.Tests;

using SourceGenerateHelper.Testing;

public sealed class PipelineCacheTests
{
    private const string Source =
        """
        namespace TestFunctions;

        using AzureFunctionsExtension.Annotations;
        using Microsoft.AspNetCore.Mvc;

        [AzureFunction]
        public sealed partial class SampleFunction
        {
            [HttpEndpoint("get", "sample")]
            public IActionResult Run()
            {
                return new EmptyResult();
            }
        }
        """;

    private const string UnrelatedSource =
        """
        namespace Other;

        internal sealed class Unrelated;
        """;

    private const string FilterSource =
        """
        namespace TestFunctions;

        using System.Threading.Tasks;
        using AzureFunctionsExtension.Annotations;
        using AzureFunctionsExtension.Filters;
        using Microsoft.AspNetCore.Mvc;

        public sealed class SampleFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next) => next(context);
        }

        [AzureFunction]
        public sealed partial class SampleFunction
        {
            [HttpEndpoint("get", "sample")]
            public IActionResult Run()
            {
                return new EmptyResult();
            }
        }
        """;

    private const string AssemblyFilterSource =
        """
        [assembly: AzureFunctionsExtension.Annotations.Filter<TestFunctions.SampleFilter>]
        """;

    private const string AddedTargetSource =
        """
        namespace TestFunctions;

        using AzureFunctionsExtension.Annotations;
        using Microsoft.AspNetCore.Mvc;

        [AzureFunction]
        public sealed partial class AddedFunction
        {
            [HttpEndpoint("get", "added")]
            public IActionResult Added()
            {
                return new EmptyResult();
            }
        }
        """;

    // ------------------------------------------------------------
    // Cache
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsModelCached()
    {
        // Arrange & Act
        var result = CompilationHelper.RunIncremental(Source, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void TargetEditRebuildsModel()
    {
        // Arrange & Act
        var result = CompilationHelper.RunIncremental(Source, AddedTargetSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void AssemblyFilterInOtherFileRebuildsModel()
    {
        // Arrange & Act
        var result = CompilationHelper.RunIncremental(FilterSource, AssemblyFilterSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
        Assert.DoesNotContain("SampleFilter", result.FirstGeneratedText, StringComparison.Ordinal);
        Assert.Contains("SampleFilter", result.SecondGeneratedText, StringComparison.Ordinal);
    }
}
