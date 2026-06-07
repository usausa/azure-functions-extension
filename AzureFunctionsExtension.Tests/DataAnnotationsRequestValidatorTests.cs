#pragma warning disable CA1707
namespace AzureFunctionsExtension.Tests;

using System.ComponentModel.DataAnnotations;

using AzureFunctionsExtension.Validation;

using Xunit;

public sealed class DataAnnotationsRequestValidatorTests
{
    [Fact]
    public void Validate_ValidObject_ReturnsTrue()
    {
        var validator = new DataAnnotationsRequestValidator();

        Assert.True(validator.Validate(new SampleRequest { Name = "abc", Age = 50 }));
    }

    [Fact]
    public void Validate_MissingRequired_ReturnsFalse()
    {
        var validator = new DataAnnotationsRequestValidator();

        Assert.False(validator.Validate(new SampleRequest { Name = string.Empty, Age = 50 }));
    }

    [Fact]
    public void Validate_StringExceedsMaxLength_ReturnsFalse()
    {
        var validator = new DataAnnotationsRequestValidator();

        Assert.False(validator.Validate(new SampleRequest { Name = "abcdef", Age = 50 }));
    }

    [Fact]
    public void Validate_ValueOutOfRange_ReturnsFalse()
    {
        var validator = new DataAnnotationsRequestValidator();

        Assert.False(validator.Validate(new SampleRequest { Name = "abc", Age = 0 }));
    }

    [Fact]
    public void Validate_TypeWithoutAnnotations_ReturnsTrue()
    {
        var validator = new DataAnnotationsRequestValidator();

        Assert.True(validator.Validate(new PlainRequest { Value = 123 }));
    }

    private sealed class SampleRequest
    {
        [Required]
        [StringLength(5)]
        public string Name { get; set; } = string.Empty;

        [Range(1, 100)]
        public int Age { get; set; }
    }

    private sealed class PlainRequest
    {
        public int Value { get; set; }
    }
}
