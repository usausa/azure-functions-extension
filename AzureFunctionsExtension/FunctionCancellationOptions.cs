namespace AzureFunctionsExtension;

using System.Threading;

public sealed class FunctionCancellationOptions
{
    public TimeSpan Timeout { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;

    public CancellationTokenSource CreateTokenSource(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(Timeout);
        return source;
    }
}
