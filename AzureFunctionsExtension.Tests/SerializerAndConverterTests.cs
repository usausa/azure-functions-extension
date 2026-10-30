namespace AzureFunctionsExtension.Tests;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Binders;
using AzureFunctionsExtension.Serialization;

using Microsoft.Extensions.DependencyInjection;

public sealed class SerializerAndConverterTests
{
    //--------------------------------------------------------------------------------
    // DI integration
    //--------------------------------------------------------------------------------

    private sealed class SnakeCasePayload
    {
        public string UserName { get; set; } = string.Empty;
    }

    private sealed record ValuePayload
    {
        public int Value { get; set; }
    }

    private sealed class CustomBodySerializer : IBodySerializer
    {
        public ValueTask<T?> DeserializeAsync<T>(Stream body, CancellationToken cancellationToken) => default;

        public Task SerializeAsync<T>(Stream output, T value, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task ConfiguredNamingPolicyIsAppliedToDeserialization()
    {
        var services = new ServiceCollection();
        services.AddAzureFunctionExtension(static o => o.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        await using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IBodySerializer>();

        using var stream = new MemoryStream("""{"user_name":"abc"}"""u8.ToArray());
        var result = await serializer.DeserializeAsync<SnakeCasePayload>(stream, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("abc", result.UserName);
    }

    [Fact]
    public async Task ConfiguredNamingPolicyIsAppliedToSerialization()
    {
        var services = new ServiceCollection();
        services.AddAzureFunctionExtension(static o => o.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        await using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IBodySerializer>();

        using var stream = new MemoryStream();
        await serializer.SerializeAsync(stream, new SnakeCasePayload { UserName = "abc" }, CancellationToken.None);

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("user_name", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultRegistrationUsesWebDefaultsCaseInsensitive()
    {
        var services = new ServiceCollection();
        services.AddAzureFunctionExtension();

        await using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IBodySerializer>();

        using var stream = new MemoryStream("""{"VALUE":1}"""u8.ToArray());
        var result = await serializer.DeserializeAsync<ValuePayload>(stream, CancellationToken.None);

        Assert.Equal(new ValuePayload { Value = 1 }, result);
    }

    [Fact]
    public void ExistingBodySerializerIsNotOverwritten()
    {
        var services = new ServiceCollection();
        var custom = new CustomBodySerializer();
        services.AddSingleton<IBodySerializer>(custom);
        services.AddAzureFunctionExtension();

        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IBodySerializer>();

        Assert.Same(custom, serializer);
    }

    //--------------------------------------------------------------------------------
    // JsonBodySerializer.Default caching
    //--------------------------------------------------------------------------------

    [Fact]
    public void DefaultReturnsSameInstance()
    {
        var first = JsonBodySerializer.Default;
        var second = JsonBodySerializer.Default;

        Assert.Same(first, second);
    }

    //--------------------------------------------------------------------------------
    // JsonBodySerializer: asynchronous reading
    //--------------------------------------------------------------------------------

    [Fact]
    public async Task DeserializeAsyncDoesNotReadSynchronously()
    {
        var serializer = new JsonBodySerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var stream = new SyncReadDisallowedStream("""{"value":1}"""u8);

        var result = await serializer.DeserializeAsync<ValuePayload>(stream, CancellationToken.None);

        Assert.Equal(new ValuePayload { Value = 1 }, result);
    }

    [Fact]
    public async Task DeserializeAsyncWithContextDoesNotReadSynchronously()
    {
        var serializer = new JsonBodySerializer(BodyJsonContext.Default);
        await using var stream = new SyncReadDisallowedStream("""{"name":"abc"}"""u8);

        var result = await serializer.DeserializeAsync<ContextPayload>(stream, CancellationToken.None);

        Assert.Equal(new ContextPayload("abc"), result);
    }

    //--------------------------------------------------------------------------------
    // StringConverter culture independence
    //--------------------------------------------------------------------------------

    [Fact]
    public void StringConverterParsesUsingInvariantCultureRegardlessOfCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.True(StringConverter.TryToDouble("1.5", out var doubleResult));
            Assert.Equal(1.5, doubleResult);

            Assert.True(StringConverter.TryToDateTime("2026-06-10T01:02:03", out var dateResult));
            Assert.Equal(new DateTime(2026, 6, 10, 1, 2, 3, DateTimeKind.Unspecified), dateResult);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}

internal sealed record ContextPayload(string Name);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ContextPayload))]
internal sealed partial class BodyJsonContext : JsonSerializerContext;
