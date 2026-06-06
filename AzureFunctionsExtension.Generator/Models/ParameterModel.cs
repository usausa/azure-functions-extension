namespace AzureFunctionsExtension.Generator.Models;

internal sealed record ParameterModel(
    string Name,
    TypeRefModel Type,
    ParameterBindingKind BindingKind,
    string Key,
    string ConverterMethod,
    bool SkipValidation,
    bool HasDefault,
    string? DefaultValueLiteral);

internal enum ParameterBindingKind
{
    HttpRequest,
    FunctionContext,
    Logger,
    CancellationToken,
    FromQuery,
    FromHeader,
    FromRoute,
    FromBody,
    FromServices,
    FromTrigger
}
