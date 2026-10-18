namespace AzureFunctionsExtension.Tests;

using AzureFunctionsExtension.Hosting;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class HostingTests
{
    private static readonly IServiceProvider EmptyServices = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void TransformerAddsMetadataOfCatalogsInOrder()
    {
        var plain = CreateMetadata("App.Plain.Run");
        List<IFunctionMetadata> metadata = [plain];
        var transformer = new ExtensionFunctionMetadataTransformer(
            [new FakeCatalog("App.First.Run_Handler"), new FakeCatalog("App.Second.Run_Handler", "App.Third.Run_Handler")]);

        transformer.Transform(metadata);

        string[] expected = ["App.Plain.Run", "App.First.Run_Handler", "App.Second.Run_Handler", "App.Third.Run_Handler"];
        Assert.Equal(expected, metadata.Select(static x => x.EntryPoint));
        Assert.Same(plain, metadata[0]);
    }

    [Fact]
    public void TransformerSkipsFunctionAlreadyProvided()
    {
        var provided = CreateMetadata("App.First.Run_Handler");
        List<IFunctionMetadata> metadata = [provided];
        var transformer = new ExtensionFunctionMetadataTransformer([new FakeCatalog("App.First.Run_Handler", "App.Second.Run_Handler")]);

        transformer.Transform(metadata);

        string[] expected = ["App.First.Run_Handler", "App.Second.Run_Handler"];
        Assert.Equal(expected, metadata.Select(static x => x.EntryPoint));
        Assert.Same(provided, metadata[0]);
    }

    [Fact]
    public async Task ExecutorUsesCatalogWithoutCallingInner()
    {
        var catalog = new FakeCatalog("App.First.Run_Handler");
        var executor = new ExtensionFunctionExecutor(new MarkingExecutor(new ExecutorMarker("inner")), new ExtensionFunctionExecutorState(), [catalog]);
        var context = new FakeFunctionContext(EmptyServices, "App.First.Run_Handler");

        await executor.ExecuteAsync(context);

        string[] expected = ["App.First.Run_Handler"];
        Assert.Equal(expected, catalog.Executed);
        Assert.Empty(context.Items);
    }

    [Fact]
    public async Task ExecutorDelegatesToInnerWhenNoCatalogHandles()
    {
        var catalog = new FakeCatalog("App.First.Run_Handler");
        var executor = new ExtensionFunctionExecutor(new MarkingExecutor(new ExecutorMarker("inner")), new ExtensionFunctionExecutorState(), [catalog]);
        var context = new FakeFunctionContext(EmptyServices, "App.Plain.Run");

        await executor.ExecuteAsync(context);

        Assert.Empty(catalog.Executed);
        Assert.Equal("inner", context.Items[MarkingExecutor.Key]);
    }

    [Fact]
    public async Task ExecutorThrowsWhenNoInnerAndNoCatalogHandles()
    {
        var executor = new ExtensionFunctionExecutor(null, new ExtensionFunctionExecutorState(), [new FakeCatalog("App.First.Run_Handler")]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await executor.ExecuteAsync(new FakeFunctionContext(EmptyServices, "App.Plain.Run")));

        Assert.Contains("App.Plain.Run", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("instance")]
    [InlineData("factory")]
    [InlineData("type")]
    public async Task RegistrationWrapsPreviousExecutor(string registration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutorMarker(registration));
        switch (registration)
        {
            case "instance":
                services.AddSingleton<IFunctionExecutor>(new MarkingExecutor(new ExecutorMarker(registration)));
                break;
            case "factory":
                services.AddSingleton<IFunctionExecutor>(static sp => new MarkingExecutor(sp.GetRequiredService<ExecutorMarker>()));
                break;
            default:
                services.AddSingleton<IFunctionExecutor, MarkingExecutor>();
                break;
        }

        services.AddAzureFunctionExtension();

        await using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IFunctionExecutor>();
        var context = new FakeFunctionContext(provider, "App.Plain.Run");
        await executor.ExecuteAsync(context);

        Assert.IsType<ExtensionFunctionExecutor>(executor);
        Assert.Equal(registration, context.Items[MarkingExecutor.Key]);
    }

    [Fact]
    public async Task RegistrationWrapsPreviousExecutorRegisteredByType()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExecutorMarker("last"));
        services.AddSingleton<IFunctionExecutor>(new MarkingExecutor(new ExecutorMarker("first")));
        services.AddSingleton<IFunctionExecutor, MarkingExecutor>();
        services.AddAzureFunctionExtension();

        await using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IFunctionExecutor>();
        var context = new FakeFunctionContext(provider, "App.Plain.Run");
        await executor.ExecuteAsync(context);

        Assert.IsType<ExtensionFunctionExecutor>(executor);
        Assert.Equal("last", context.Items[MarkingExecutor.Key]);
    }

    [Fact]
    public async Task RegistrationIsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFunctionExecutor>(new MarkingExecutor(new ExecutorMarker("inner")));
        services.AddAzureFunctionExtension();
        services.AddAzureFunctionExtension();

        Assert.Single(services, static x => x.ServiceType == typeof(IFunctionMetadataTransformer));
        Assert.Equal(2, services.Count(static x => x.ServiceType == typeof(IFunctionExecutor)));

        await using var provider = services.BuildServiceProvider();
        var context = new FakeFunctionContext(provider, "App.Plain.Run");
        await provider.GetRequiredService<IFunctionExecutor>().ExecuteAsync(context);

        Assert.Equal("inner", context.Items[MarkingExecutor.Key]);
    }

    [Fact]
    public async Task RegistrationIgnoresKeyedExecutor()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IFunctionExecutor>("other", new MarkingExecutor(new ExecutorMarker("keyed")));
        services.AddAzureFunctionExtension();

        await using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IFunctionExecutor>();

        Assert.IsType<ExtensionFunctionExecutor>(executor);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await executor.ExecuteAsync(new FakeFunctionContext(provider, "App.Plain.Run")));
    }

    [Fact]
    public void RegistryIgnoresSameCatalogType()
    {
        GeneratedFunctionCatalog.Register(new RegistryCatalog());
        GeneratedFunctionCatalog.Register(new RegistryCatalog());

        Assert.Single(GeneratedFunctionCatalog.Catalogs, static x => x is RegistryCatalog);
    }

    [Fact]
    public async Task ValidatorSucceedsWhenExtensionExecutorIsUsed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFunctionExecutor>(new MarkingExecutor(new ExecutorMarker("inner")));
        services.AddAzureFunctionExtension();

        await using var provider = services.BuildServiceProvider();

        await new ExtensionStartupValidator(provider).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ValidatorThrowsWhenNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFunctionExecutor>(new MarkingExecutor(new ExecutorMarker("inner")));

        await using var provider = services.BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new ExtensionStartupValidator(provider).StartAsync(CancellationToken.None));
        Assert.Contains("AddAzureFunctionExtension()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidatorThrowsWhenExecutorIsReplacedAfterRegistration()
    {
        var services = new ServiceCollection();
        services.AddAzureFunctionExtension();
        services.AddSingleton<IFunctionExecutor>(new MarkingExecutor(new ExecutorMarker("after")));

        await using var provider = services.BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new ExtensionStartupValidator(provider).StartAsync(CancellationToken.None));
        Assert.Contains("after ConfigureFunctionsWebApplication()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureStartupRegistersValidator()
    {
        var builder = new HostBuilder();
        GeneratedFunctionCatalog.ConfigureStartup(builder);

        using var host = builder.Build();

        Assert.Contains(host.Services.GetServices<IHostedService>(), static x => x is ExtensionStartupValidator);
    }

    private static DefaultFunctionMetadata CreateMetadata(string entryPoint) =>
        new()
        {
            Language = "dotnet-isolated",
            Name = entryPoint,
            EntryPoint = entryPoint,
            ScriptFile = "App.dll",
            RawBindings = []
        };

    private sealed class FakeCatalog : IGeneratedFunctionCatalog
    {
        private readonly string[] entryPoints;

        public FakeCatalog(params string[] entryPoints)
        {
            this.entryPoints = entryPoints;
        }

        public List<string> Executed { get; } = [];

        public void AddMetadata(IList<IFunctionMetadata> metadata)
        {
            foreach (var entryPoint in entryPoints)
            {
                metadata.Add(CreateMetadata(entryPoint));
            }
        }

        public ValueTask<bool> TryExecuteAsync(FunctionContext context)
        {
            var entryPoint = context.FunctionDefinition.EntryPoint;
            if (!entryPoints.Contains(entryPoint))
            {
                return new ValueTask<bool>(false);
            }

            Executed.Add(entryPoint);
            return new ValueTask<bool>(true);
        }
    }

    private sealed class RegistryCatalog : IGeneratedFunctionCatalog
    {
        public void AddMetadata(IList<IFunctionMetadata> metadata)
        {
        }

        public ValueTask<bool> TryExecuteAsync(FunctionContext context) => new(false);
    }

    private sealed record ExecutorMarker(string Value);

    private sealed class MarkingExecutor : IFunctionExecutor
    {
        public const string Key = "executor";

        private readonly ExecutorMarker marker;

        public MarkingExecutor(ExecutorMarker marker)
        {
            this.marker = marker;
        }

        public ValueTask ExecuteAsync(FunctionContext context)
        {
            context.Items[Key] = marker.Value;
            return ValueTask.CompletedTask;
        }
    }
}
