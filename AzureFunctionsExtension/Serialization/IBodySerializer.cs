namespace AzureFunctionsExtension.Serialization;

public interface IBodySerializer
{
    System.Threading.Tasks.ValueTask<T?> DeserializeAsync<T>(System.IO.Stream body, System.Threading.CancellationToken cancellationToken);

    System.Threading.Tasks.Task SerializeAsync<T>(System.IO.Stream output, T value, System.Threading.CancellationToken ct);
}
