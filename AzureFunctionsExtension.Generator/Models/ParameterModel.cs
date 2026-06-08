namespace AzureFunctionsExtension.Generator.Models;

internal enum ParameterBindingType
{
    HttpRequest,
    Context,
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

internal static class ParameterModelExtensions
{
    public static bool IsNullableBodyParameter(this ParameterModel param)
        => param.Type.IsNullable || (param.Type.IsReferenceType && param.Type.IsNullableReferenceType);
}
