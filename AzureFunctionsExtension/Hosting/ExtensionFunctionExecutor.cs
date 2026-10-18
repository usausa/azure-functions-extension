namespace AzureFunctionsExtension.Hosting;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Invocation;

internal sealed class ExtensionFunctionExecutor : IFunctionExecutor
{
    private readonly IFunctionExecutor? inner;
    private readonly IReadOnlyList<IGeneratedFunctionCatalog>? catalogs;

    public ExtensionFunctionExecutor(IFunctionExecutor? inner, ExtensionFunctionExecutorState state, IReadOnlyList<IGeneratedFunctionCatalog>? catalogs = null)
    {
        this.inner = inner;
        this.catalogs = catalogs;
        state.Attached = true;
    }

    public async ValueTask ExecuteAsync(FunctionContext context)
    {
        foreach (var catalog in catalogs ?? GeneratedFunctionCatalog.Catalogs)
        {
            if (await catalog.TryExecuteAsync(context).ConfigureAwait(false))
            {
                return;
            }
        }

        if (inner is null)
        {
            throw new InvalidOperationException($"Function is not found. entryPoint=[{context.FunctionDefinition.EntryPoint}]");
        }

        await inner.ExecuteAsync(context).ConfigureAwait(false);
    }
}
