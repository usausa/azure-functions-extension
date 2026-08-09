namespace AzureFunctionsExtension.Validation;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

[RequiresUnreferencedCode("DataAnnotations validation uses reflection over instance properties.")]
public sealed class DataAnnotationsRequestValidator : IRequestValidator
{
    public bool Validate(object value)
    {
        var context = new ValidationContext(value);
        return Validator.TryValidateObject(value, context, null, true);
    }

    public bool Validate(object value, ICollection<ValidationResult> results)
    {
        var context = new ValidationContext(value);
        return Validator.TryValidateObject(value, context, results, true);
    }
}
