namespace AzureFunctionsExtension.Tests;

using System.ComponentModel.DataAnnotations;

using AzureFunctionsExtension.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;

[ValidatableType]
public sealed class ValidatableRequest
{
    [Required]
    public string? Name { get; set; }

    [Range(1, 10)]
    public int Count { get; set; }

    public ValidatableChild? Child { get; set; }

    public IReadOnlyList<ValidatableChild>? Items { get; set; }
}

public sealed class ValidatableChild
{
    [Required]
    public string? Value { get; set; }
}

public sealed class UnmarkedRequest
{
    [Required]
    public string? Name { get; set; }
}

public sealed class ValidatableTypeRequestValidatorTests
{
    private static ValidatableTypeRequestValidator CreateValidator()
    {
        var services = new ServiceCollection();
        services.AddValidation();
        using var provider = services.BuildServiceProvider();
        return new ValidatableTypeRequestValidator(provider.GetRequiredService<IOptions<ValidationOptions>>());
    }

    [Fact]
    public void ValidateValidObjectReturnsTrue()
    {
        var validator = CreateValidator();
        var value = new ValidatableRequest { Name = "a", Count = 1 };
        var results = new List<ValidationResult>();

        Assert.True(validator.Validate(value));
        Assert.True(validator.Validate(value, results));
        Assert.Empty(results);
    }

    [Fact]
    public void ValidateCollectsMemberDetails()
    {
        var validator = CreateValidator();
        var value = new ValidatableRequest { Count = 0 };
        var results = new List<ValidationResult>();

        Assert.False(validator.Validate(value));
        Assert.False(validator.Validate(value, results));

        Assert.Equal(["Name", "Count"], results.Select(static x => x.MemberNames.Single()));
        Assert.Equal(["The Name field is required.", "The field Count must be between 1 and 10."], results.Select(static x => x.ErrorMessage));
    }

    [Fact]
    public void ValidateNestedObjectsUsesPathAsMemberName()
    {
        var validator = CreateValidator();
        var value = new ValidatableRequest
        {
            Name = "a",
            Count = 1,
            Child = new ValidatableChild(),
            Items = [new ValidatableChild { Value = "v" }, new ValidatableChild()]
        };
        var results = new List<ValidationResult>();

        Assert.False(validator.Validate(value, results));

        Assert.Equal(["Child.Value", "Items[1].Value"], results.Select(static x => x.MemberNames.Single()));
    }

    [Fact]
    public void ValidateUnknownTypeThrows()
    {
        var validator = CreateValidator();

        var ex = Assert.Throws<InvalidOperationException>(() => validator.Validate(new UnmarkedRequest()));

        Assert.Contains(typeof(UnmarkedRequest).FullName!, ex.Message, StringComparison.Ordinal);
    }
}
