namespace AzureFunctionsExtension.Generator.Models;

internal enum ParameterBindingType
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

internal sealed record ParameterModel(
    string Name,
    TypeRefModel Type,
    ParameterBindingType BindingType,
    string Key,
    string ConverterMethod,
    bool SkipValidation,
    bool HasDefault,
    string? DefaultValueLiteral);
