namespace AzureFunctionsExtension.Generator.Models;

using SourceGenerateHelper;

internal enum HandlerType
{
    Http,
    Timer,
    Queue
}

internal enum ResponseType
{
    Poco,
    ActionResult
}

internal sealed record HandlerModel(
    string MethodName,
    HandlerType Type,
    bool IsAsync,
    TypeRefModel? ResultType,
    ResponseType ResponseType,
    EquatableArray<ParameterModel> Parameters,
    string? HttpMethod,
    string? Route,
    string? AuthorizationLevel,
    string? TimerSchedule,
    string? QueueName,
    string? QueueConnection);
