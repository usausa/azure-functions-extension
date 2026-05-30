namespace AzureFunctionsExtension.Validation;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

[RequiresUnreferencedCode("DataAnnotations validation uses reflection over instance properties.")]
public sealed class DataAnnotationsRequestValidator : IRequestValidator
{
    public bool Validate(object value)
    {
        var context = new ValidationContext(value);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(value, context, results, true);
    }
}
