namespace AzureFunctionsExtension.Example.Tests;

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Azure.Functions.Worker;

internal sealed class FakeFunctionContext : FunctionContext
{
    public FakeFunctionContext(IServiceProvider instanceServices)
    {
        InstanceServices = instanceServices;
    }

    public override string InvocationId => "test-invocation";

    public override string FunctionId => "test-function";

    public override TraceContext TraceContext => throw new NotSupportedException();

    public override BindingContext BindingContext => throw new NotSupportedException();

    public override RetryContext RetryContext => throw new NotSupportedException();

    public override IServiceProvider InstanceServices { get; set; }

    public override FunctionDefinition FunctionDefinition => throw new NotSupportedException();

    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

    public override IInvocationFeatures Features => throw new NotSupportedException();

    public override CancellationToken CancellationToken => CancellationToken.None;
}
