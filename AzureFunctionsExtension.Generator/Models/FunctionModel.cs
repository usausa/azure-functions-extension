namespace AzureFunctionsExtension.Generator.Models;

using SourceGenerateHelper;

internal sealed record FunctionModel(
    string Namespace,
    string ClassName,
    bool IsValueType,
    TypeRefModel FunctionType,
    EquatableArray<TypeRefModel> ConstructorParameters,
    ServiceResolverModel? ServiceResolver,
    EquatableArray<FilterDescriptorModel> Filters,
    EquatableArray<HandlerModel> Handlers);
