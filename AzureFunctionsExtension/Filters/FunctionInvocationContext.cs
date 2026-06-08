namespace AzureFunctionsExtension.Filters;

using System.Threading;

using Microsoft.Azure.Functions.Worker;

public sealed class FunctionInvocationContext
{
    public object? Request { get; init; }

    public FunctionContext FunctionContext { get; init; } = default!;

    public CancellationToken CancellationToken { get; init; }

    public object? Result { get; set; }

    private Dictionary<string, object?>? items;

    public IDictionary<string, object?> Items => items ??= [];
}
