namespace AzureFunctionsExtension.Hosting;

using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;

internal sealed class ExtensionFunctionMetadataTransformer : IFunctionMetadataTransformer
{
    private readonly IReadOnlyList<IGeneratedFunctionCatalog>? catalogs;

    public ExtensionFunctionMetadataTransformer(IReadOnlyList<IGeneratedFunctionCatalog>? catalogs = null)
    {
        this.catalogs = catalogs;
    }

    public string Name => "AzureFunctionsExtension";

    public void Transform(IList<IFunctionMetadata> original)
    {
        var existing = new HashSet<string>(original.Select(static x => x.EntryPoint).OfType<string>(), StringComparer.Ordinal);
        var added = new List<IFunctionMetadata>();
        foreach (var catalog in catalogs ?? GeneratedFunctionCatalog.Catalogs)
        {
            catalog.AddMetadata(added);
        }

        foreach (var metadata in added)
        {
            if ((metadata.EntryPoint is null) || existing.Add(metadata.EntryPoint))
            {
                original.Add(metadata);
            }
        }
    }
}
