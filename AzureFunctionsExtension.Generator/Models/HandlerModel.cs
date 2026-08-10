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
    // Method signature
    string MethodName,
    HandlerType Type,
    bool IsAsync,
    TypeRefModel? ResultType,
    ResponseType ResponseType,
    EquatableArray<ParameterModel> Parameters,
    // Http trigger
    string? HttpMethod,
    string? Route,
    string? AuthorizationLevel,
    // Timer trigger
    string? TimerSchedule,
    // Queue trigger
    string? QueueName,
    string? QueueConnection);
