namespace AzureFunctionsExtension.Generator.Models;

internal sealed record FilterDescriptorModel(
    TypeRefModel FilterType,
    int Order,
    int Index);
