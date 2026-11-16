namespace AzureFunctionsExtension.Example.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AzureFunctionsExtension.Example.Functions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Extensions.DependencyInjection;

public sealed class FunctionCatalogTests
{
    private static readonly string[] EntryPoints =
    [
        "AzureFunctionsExtension.Example.Functions.FilterFunction.Ping_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.Array_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.Body_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.GetItem_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.Greet_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.HeaderArray_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.HeaderEcho_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.Lookup_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.Query_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.RouteArray_Handler",
        "AzureFunctionsExtension.Example.Functions.HttpFunction.Timer_Handler",
        "AzureFunctionsExtension.Example.Functions.QueueFunction.ProcessMessage_Handler",
        "AzureFunctionsExtension.Example.Functions.UserFunction.Me_Handler"
    ];

    public FunctionCatalogTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(HttpFunction).Module.ModuleHandle);
    }

    [Fact]
    public void MetadataContainsAllFunctions()
    {
        var metadata = CreateMetadata();

        Assert.Equal(EntryPoints, metadata.Select(static x => x.EntryPoint).Order(StringComparer.Ordinal));
        Assert.All(metadata, static x => Assert.Equal("AzureFunctionsExtension.Example.dll", x.ScriptFile));
        Assert.All(metadata, static x => Assert.Equal("dotnet-isolated", x.Language));
        Assert.Equal(metadata.Count, metadata.Select(static x => x.FunctionId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void BindingsArePlainJsonWithExpectedTrigger()
    {
        foreach (var metadata in CreateMetadata())
        {
            string[] expected = metadata.Name switch
            {
                "Timer" => ["timerTrigger"],
                "ProcessMessage" => ["queueTrigger"],
                _ => ["httpTrigger", "http"]
            };
            Assert.Equal(expected, metadata.RawBindings!.Select(GetBindingType));
        }
    }

    [Fact]
    public void EntryPointsResolveToPublicStaticMethods()
    {
        var assembly = typeof(HttpFunction).Assembly;
        foreach (var metadata in CreateMetadata())
        {
            var entryPoint = metadata.EntryPoint!;
            var separator = entryPoint.LastIndexOf('.');
            var type = assembly.GetType(entryPoint[..separator]);
            Assert.NotNull(type);

            var method = type.GetMethod(entryPoint[(separator + 1)..]);
            Assert.NotNull(method);
            Assert.True(method.IsPublic);
            Assert.True(method.IsStatic);
            Assert.Equal(metadata.Name, method.GetCustomAttribute<FunctionAttribute>()?.Name);
        }
    }

    [Fact]
    public Task ExecutorDispatchesQueueFunction()
    {
        var services = HandlerTestHost.CreateServices();
        var context = new FakeFunctionContext(services, "AzureFunctionsExtension.Example.Functions.QueueFunction.ProcessMessage_Handler", "hello");

        return services.GetRequiredService<IFunctionExecutor>().ExecuteAsync(context).AsTask();
    }

    [Fact]
    public Task ExecutorDispatchesTimerFunction()
    {
        var services = HandlerTestHost.CreateServices();
        var context = new FakeFunctionContext(services, "AzureFunctionsExtension.Example.Functions.HttpFunction.Timer_Handler", new TimerInfo());

        return services.GetRequiredService<IFunctionExecutor>().ExecuteAsync(context).AsTask();
    }

    [Fact]
    public Task ExecutorThrowsForUnknownEntryPoint()
    {
        var services = HandlerTestHost.CreateServices();
        var context = new FakeFunctionContext(services, "AzureFunctionsExtension.Example.Functions.UnknownFunction.Run_Handler");

        return Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IFunctionExecutor>().ExecuteAsync(context).AsTask());
    }

    private static List<IFunctionMetadata> CreateMetadata()
    {
        var metadata = new List<IFunctionMetadata>();
        HandlerTestHost.CreateServices().GetRequiredService<IFunctionMetadataTransformer>().Transform(metadata);
        return metadata;
    }

    private static string? GetBindingType(string binding)
    {
        using var document = JsonDocument.Parse(binding);
        return document.RootElement.GetProperty("type").GetString();
    }
}
