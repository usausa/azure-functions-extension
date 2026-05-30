namespace AzureFunctionsExtension.Annotations;

using Microsoft.Azure.Functions.Worker;

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpEndpointAttribute : Attribute
{
    public string Method { get; }

    public string Route { get; }

    public AuthorizationLevel AuthorizationLevel { get; }

    public HttpEndpointAttribute(string method, string route, AuthorizationLevel authorizationLevel = AuthorizationLevel.Function)
    {
        Method = method;
        Route = route;
        AuthorizationLevel = authorizationLevel;
    }
}
