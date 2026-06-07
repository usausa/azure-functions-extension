#pragma warning disable CA1707
namespace AzureFunctionsExtension.Tests;

using System.Text.Json.Serialization;

using AzureFunctionsExtension.Serialization;

using Xunit;

public sealed class JsonBodySerializerTests
{
    [Fact]
    public void Deserialize_WithOptions_ReadsCamelCaseJson()
    {
        var serializer = JsonBodySerializer.Default;

        using var stream = new MemoryStream("{\"id\":7,\"name\":\"abc\"}"u8.ToArray());
        var result = serializer.Deserialize<SerializerSampleModel>(stream);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("abc", result.Name);
    }

    [Fact]
    public async Task SerializeAsync_WithOptions_UsesCamelCaseAndOmitsNull()
    {
        var serializer = JsonBodySerializer.Default;

        using var stream = new MemoryStream();
        await serializer.SerializeAsync(stream, new SerializerSampleModel { Id = 7, Name = null }, CancellationToken.None);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("\"id\":7", json, StringComparison.Ordinal);
        Assert.DoesNotContain("name", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SerializeAsync_WithOptions_RoundTrips()
    {
        var serializer = JsonBodySerializer.Default;

        using var output = new MemoryStream();
        await serializer.SerializeAsync(output, new SerializerSampleModel { Id = 3, Name = "x" }, CancellationToken.None);

        using var input = new MemoryStream(output.ToArray());
        var result = serializer.Deserialize<SerializerSampleModel>(input);

        Assert.NotNull(result);
        Assert.Equal(3, result.Id);
        Assert.Equal("x", result.Name);
    }

    [Fact]
    public void Deserialize_WithContext_ReadsCamelCaseJson()
    {
        var serializer = new JsonBodySerializer(SerializerJsonContext.Default);

        using var stream = new MemoryStream("{\"id\":7,\"name\":\"abc\"}"u8.ToArray());
        var result = serializer.Deserialize<SerializerSampleModel>(stream);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("abc", result.Name);
    }

    [Fact]
    public async Task SerializeAsync_WithContext_UsesCamelCaseAndOmitsNull()
    {
        var serializer = new JsonBodySerializer(SerializerJsonContext.Default);

        using var stream = new MemoryStream();
        await serializer.SerializeAsync(stream, new SerializerSampleModel { Id = 7, Name = null }, CancellationToken.None);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("\"id\":7", json, StringComparison.Ordinal);
        Assert.DoesNotContain("name", json, StringComparison.Ordinal);
    }
}

internal sealed class SerializerSampleModel
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SerializerSampleModel))]
internal sealed partial class SerializerJsonContext : JsonSerializerContext
{
}
