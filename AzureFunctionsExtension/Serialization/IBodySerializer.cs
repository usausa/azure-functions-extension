namespace AzureFunctionsExtension.Serialization;

public interface IBodySerializer
{
    T? Deserialize<T>(System.IO.Stream body);

    System.Threading.Tasks.Task SerializeAsync<T>(System.IO.Stream output, T value, System.Threading.CancellationToken ct);
}
