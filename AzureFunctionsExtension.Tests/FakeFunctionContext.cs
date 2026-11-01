namespace AzureFunctionsExtension.Tests;

using System.Collections;
using System.Collections.Immutable;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Context.Features;

internal sealed class FakeFunctionContext : FunctionContext
{
    private readonly FakeInvocationFeatures? features;

    public FakeFunctionContext(IServiceProvider instanceServices, CancellationToken cancellationToken = default)
    {
        InstanceServices = instanceServices;
        CancellationToken = cancellationToken;
    }

    public FakeFunctionContext(IServiceProvider instanceServices, string entryPoint, params object?[] inputs)
        : this(instanceServices)
    {
        FunctionDefinition = new FakeFunctionDefinition(entryPoint);
        features = new FakeInvocationFeatures();
        features.Set<IFunctionInputBindingFeature>(new FakeInputBindingFeature(inputs));
    }

    public override string InvocationId => "test-invocation";

    public override string FunctionId => "test-function";

    public override TraceContext TraceContext => throw new NotSupportedException();

    public override BindingContext BindingContext => throw new NotSupportedException();

    public override RetryContext RetryContext => throw new NotSupportedException();

    public override IServiceProvider InstanceServices { get; set; }

    public override FunctionDefinition FunctionDefinition => field ?? throw new NotSupportedException();

    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

    public override IInvocationFeatures Features => features ?? throw new NotSupportedException();

    public override CancellationToken CancellationToken { get; }

    private sealed class FakeFunctionDefinition : FunctionDefinition
    {
        public FakeFunctionDefinition(string entryPoint)
        {
            EntryPoint = entryPoint;
        }

        public override ImmutableArray<FunctionParameter> Parameters => [];

        public override string PathToAssembly => string.Empty;

        public override string EntryPoint { get; }

        public override string Id => EntryPoint;

        public override string Name => EntryPoint;

        public override IImmutableDictionary<string, BindingMetadata> InputBindings => ImmutableDictionary<string, BindingMetadata>.Empty;

        public override IImmutableDictionary<string, BindingMetadata> OutputBindings => ImmutableDictionary<string, BindingMetadata>.Empty;
    }

    private sealed class FakeInvocationFeatures : IInvocationFeatures
    {
        private readonly Dictionary<Type, object> instances = [];

        public void Set<T>(T instance) => instances[typeof(T)] = instance!;

        public T? Get<T>() => instances.TryGetValue(typeof(T), out var instance) ? (T)instance : default;

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => instances.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class FakeInputBindingFeature : IFunctionInputBindingFeature
    {
        private readonly object?[] values;

        public FakeInputBindingFeature(object?[] values)
        {
            this.values = values;
        }

        public ValueTask<FunctionInputBindingResult> BindFunctionInputAsync(FunctionContext context) =>
            new(new FunctionInputBindingResult(values));
    }
}
