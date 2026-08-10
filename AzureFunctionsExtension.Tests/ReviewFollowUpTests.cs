namespace AzureFunctionsExtension.Tests;

using System.ComponentModel.DataAnnotations;
using System.IO;

using AzureFunctionsExtension.Binders;
using AzureFunctionsExtension.Serialization;
using AzureFunctionsExtension.Validation;

public sealed class ReviewFollowUpTests
{
    //--------------------------------------------------------------------------------
    // StringConverter: explicit DateTimeStyles
    //--------------------------------------------------------------------------------

    [Fact]
    public void TryToDateTimeUtcDesignatorKeepsUtcKind()
    {
        Assert.True(StringConverter.TryToDateTime("2026-06-10T01:02:03Z", out var result));

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(new DateTime(2026, 6, 10, 1, 2, 3, DateTimeKind.Utc), result);
    }

    [Fact]
    public void TryToDateTimeWithoutOffsetKeepsUnspecifiedKind()
    {
        Assert.True(StringConverter.TryToDateTime("2026-06-10T01:02:03", out var result));

        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    [Fact]
    public void TryToDateTimeOffsetWithoutOffsetAssumesUtc()
    {
        Assert.True(StringConverter.TryToDateTimeOffset("2026-06-10T01:02:03", out var result));

        Assert.Equal(TimeSpan.Zero, result.Offset);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 1, 2, 3, TimeSpan.Zero), result);
    }

    [Fact]
    public void TryToDateTimeOffsetWithOffsetKeepsOffset()
    {
        Assert.True(StringConverter.TryToDateTimeOffset("2026-06-10T01:02:03+09:00", out var result));

        Assert.Equal(TimeSpan.FromHours(9), result.Offset);
    }

    //--------------------------------------------------------------------------------
    // DataAnnotationsRequestValidator: detailed results
    //--------------------------------------------------------------------------------

    private sealed class ValidatedRequest
    {
        [Required]
        public string? Name { get; set; }

        [Range(1, 10)]
        public int Count { get; set; }
    }

    [Fact]
    public void ValidateCollectsMemberDetails()
    {
        var validator = new DataAnnotationsRequestValidator();
        var results = new List<ValidationResult>();

        Assert.False(validator.Validate(new ValidatedRequest { Count = 0 }, results));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, static r => r.MemberNames.Contains(nameof(ValidatedRequest.Name)));
        Assert.Contains(results, static r => r.MemberNames.Contains(nameof(ValidatedRequest.Count)));
    }

    private sealed class BoolOnlyValidator : IRequestValidator
    {
        public bool Validate(object value) => false;
    }

    [Fact]
    public void ValidateDetailOverloadFallsBackForBoolOnlyValidator()
    {
        IRequestValidator validator = new BoolOnlyValidator();
        var results = new List<ValidationResult>();

        Assert.False(validator.Validate(new ValidatedRequest(), results));
        Assert.Empty(results);
    }

    //--------------------------------------------------------------------------------
    // JsonBodySerializer.Default: aligned with AddAzureFunctionExtension defaults
    //--------------------------------------------------------------------------------

#pragma warning disable CA1812
    private sealed class NamedValue
    {
        public string? Name { get; set; }
    }
#pragma warning restore CA1812

    [Fact]
    public void DefaultDeserializesCaseInsensitively()
    {
        using var body = new MemoryStream("""{"NAME":"abc"}"""u8.ToArray());

        var value = JsonBodySerializer.Default.Deserialize<NamedValue>(body);

        Assert.Equal("abc", value?.Name);
    }
}
