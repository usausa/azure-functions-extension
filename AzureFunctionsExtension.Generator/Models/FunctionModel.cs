namespace AzureFunctionsExtension.Generator.Models;

using SourceGenerateHelper;

internal sealed record FunctionModel(
    string Namespace,
    string ClassName,
    string MetadataName,
    bool IsValueType,
    TypeRefModel FunctionType,
    EquatableArray<TypeRefModel> Filters,
    EquatableArray<HandlerModel> Handlers);
