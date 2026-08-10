namespace AzureFunctionsExtension.Validation;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public interface IRequestValidator
{
    bool Validate(object value);

    bool Validate(object value, ICollection<ValidationResult> results) => Validate(value);
}
