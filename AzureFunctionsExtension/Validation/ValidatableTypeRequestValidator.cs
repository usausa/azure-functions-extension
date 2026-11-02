namespace AzureFunctionsExtension.Validation;

using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;

public sealed class ValidatableTypeRequestValidator : IRequestValidator
{
    private readonly ValidationOptions options;

    public ValidatableTypeRequestValidator(IOptions<ValidationOptions> options)
    {
        this.options = options.Value;
    }

    public bool Validate(object value) => ValidateCore(value, null);

    public bool Validate(object value, ICollection<ValidationResult> results) => ValidateCore(value, results);

    private bool ValidateCore(object value, ICollection<ValidationResult>? results)
    {
        var type = value.GetType();
        if (!options.TryGetValidatableTypeInfo(type, out var info))
        {
            throw new InvalidOperationException($"Validatable type is not found. type=[{type.FullName}]");
        }

        var context = new ValidateContext
        {
            ValidationContext = new ValidationContext(value, type.Name, null, null),
            ValidationOptions = options
        };
        info.ValidateAsync(value, context, CancellationToken.None).GetAwaiter().GetResult();

        if (context.ValidationErrors is not { Count: > 0 } errors)
        {
            return true;
        }

        if (results is not null)
        {
            foreach (var (key, messages) in errors)
            {
                foreach (var message in messages)
                {
                    results.Add(key.Length > 0 ? new ValidationResult(message, [key]) : new ValidationResult(message));
                }
            }
        }

        return false;
    }
}
