namespace AzureFunctionsExtension.Validation;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public interface IRequestValidator
{
    bool Validate(object value);

    // Used by the generated 400 path to surface which members failed and why. The default
    // implementation keeps existing custom validators source and binary compatible.
    bool Validate(object value, ICollection<ValidationResult> results) => Validate(value);
}
