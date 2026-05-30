namespace AzureFunctionsExtension.Validation;

public interface IRequestValidator
{
    bool Validate(object value);
}
