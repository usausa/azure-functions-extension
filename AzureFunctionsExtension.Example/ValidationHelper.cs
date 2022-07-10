namespace AzureFunctionsExtension.Example;

using System.ComponentModel.DataAnnotations;

public static class ValidationHelper
{
    public static bool Validate(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var context = new ValidationContext(value);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(value, context, results, true);
    }
}
