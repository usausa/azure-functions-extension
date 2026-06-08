namespace AzureFunctionsExtension.Generator.Models;

using SourceGenerateHelper;

internal enum HandlerType
{
    Http,
    Timer,
    Queue
}

internal sealed record HandlerModel(
    string MethodName,
    HandlerType Type,
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
