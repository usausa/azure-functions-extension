namespace AzureFunctionsExtension.Generator.Models;

// スタブ ([Function] メソッド) とメタデータで同じ値を使うため、既定値の解決をここに集約する
// Centralizes default value resolution so that the stub ([Function] method) and the metadata use the same values
internal static class HandlerModelExtensions
{
    public static string GetHttpMethod(this HandlerModel handler) => handler.HttpMethod ?? "get";

    public static string GetRoute(this HandlerModel handler) => handler.Route ?? handler.MethodName;

    public static string GetAuthorizationLevel(this HandlerModel handler) => handler.AuthorizationLevel ?? "Function";

    public static string GetTimerSchedule(this HandlerModel handler) => handler.TimerSchedule ?? "0 * * * * *";

    public static string GetQueueName(this HandlerModel handler) => handler.QueueName ?? "myqueue";

    public static string GetEntryPointMethodName(this HandlerModel handler) => handler.MethodName + "_Handler";
}
