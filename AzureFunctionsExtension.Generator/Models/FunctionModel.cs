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

internal sealed record HandlerModel(
    string MethodName,
    HandlerKind Kind,
    bool IsAsync,
    TypeRefModel? ResultType,
    EquatableArray<ParameterModel> Parameters,
    string? HttpMethod,
    string? Route,
    string? AuthorizationLevel,
    string? TimerSchedule,
    string? QueueName,
    string? QueueConnection);

internal enum HandlerKind
{
    Http,
    Timer,
    Queue,
}

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
    FromTrigger,
}

internal sealed record TypeRefModel(
    string FullName,
    bool IsNullable,
    TypeRefModel? UnderlyingType,
    bool IsArray,
    TypeRefModel? ElementType);

internal sealed record FilterDescriptorModel(
    int Index,
    TypeRefModel FilterType,
    int Order);

internal sealed record ServiceResolverModel(
    TypeRefModel Type);
