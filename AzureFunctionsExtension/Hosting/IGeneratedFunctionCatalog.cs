namespace AzureFunctionsExtension.Hosting;

using System.ComponentModel;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedFunctionCatalog
{
    void AddMetadata(IList<IFunctionMetadata> metadata);

    ValueTask<bool> TryExecuteAsync(FunctionContext context);
}
