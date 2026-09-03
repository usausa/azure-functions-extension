namespace AzureFunctionsExtension.Tests;

using System.Globalization;
using System.Text;
using System.Text.Json;

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
        public T? Deserialize<T>(Stream body) => default;

        public Task SerializeAsync<T>(Stream output, T value, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void ConfiguredNamingPolicyIsAppliedToDeserialization()
    {
        var services = new ServiceCollection();
        services.AddAzureFunctionExtension(static o => o.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IBodySerializer>();

#pragma warning disable IDE0028
        using var stream = new MemoryStream("""{"user_name":"abc"}"""u8.ToArray());
#pragma warning restore IDE0028
        var result = serializer.Deserialize<SnakeCasePayload>(stream);

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
    public void DefaultRegistrationUsesWebDefaultsCaseInsensitive()
    {
        var services = new ServiceCollection();
        services.AddAzureFunctionExtension();

        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IBodySerializer>();

#pragma warning disable IDE0028
        using var stream = new MemoryStream("""{"VALUE":1}"""u8.ToArray());
#pragma warning restore IDE0028
        var result = serializer.Deserialize<ValuePayload>(stream);

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
