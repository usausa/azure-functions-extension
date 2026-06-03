namespace AzureFunctionsExtension.Generator.Models;

using SourceGenerateHelper;

internal sealed record HandlerModel(
    string MethodName,
    HandlerKind Kind,
    string? HttpMethod,
    string? Route,
    string? AuthorizationLevel,
    string? TimerSchedule,
    string? QueueName,
    string? QueueConnection,
    bool IsAsync,
    TypeRefModel? ResultType,
    bool ResultIsActionResult,
    EquatableArray<ParameterModel> Parameters);

internal enum HandlerKind
{
    Http,
    Timer,
    Queue,
}
