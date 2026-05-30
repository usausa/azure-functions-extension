namespace AzureFunctionsExtension;

#pragma warning disable CA1032
public sealed class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode)
    {
        StatusCode = statusCode;
    }

    public ApiException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
#pragma warning restore CA1032
