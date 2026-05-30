namespace AzureFunctionsExtension.Serialization;

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

public sealed class JsonBodySerializer : IBodySerializer
{
    public static JsonBodySerializer Default
    {
        [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed. Use the JsonSerializerContext overload.")]
        [RequiresDynamicCode("JSON serialization may require dynamic code generation. Use the JsonSerializerContext overload.")]
        get => new(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    private readonly JsonSerializerOptions? options;
    private readonly JsonSerializerContext? context;

    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed. Use the JsonSerializerContext overload.")]
    [RequiresDynamicCode("JSON serialization may require dynamic code generation. Use the JsonSerializerContext overload.")]
    public JsonBodySerializer(JsonSerializerOptions options)
    {
        this.options = options;
    }

    public JsonBodySerializer(JsonSerializerContext context)
    {
        this.context = context;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Reflection path is used only when constructed with JsonSerializerOptions.")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050", Justification = "Reflection path is used only when constructed with JsonSerializerOptions.")]
    public T? Deserialize<T>(Stream body)
    {
        if (context is not null)
        {
            return (T?)JsonSerializer.Deserialize(body, typeof(T), context);
        }

        return JsonSerializer.Deserialize<T>(body, options);
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Reflection path is used only when constructed with JsonSerializerOptions.")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050", Justification = "Reflection path is used only when constructed with JsonSerializerOptions.")]
    public async Task SerializeAsync<T>(Stream output, T value, CancellationToken ct)
    {
        if (context is not null)
        {
            await JsonSerializer.SerializeAsync(output, value, typeof(T), context, ct).ConfigureAwait(false);
            return;
        }

        await JsonSerializer.SerializeAsync(output, value, options, ct).ConfigureAwait(false);
    }
}
