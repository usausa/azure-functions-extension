namespace AzureFunctionsExtension.Generator.Models;

internal sealed record BuildConfigModel(
    bool UsingAzureFunctionsSdk,
    bool MetadataSourceGenEnabled);
