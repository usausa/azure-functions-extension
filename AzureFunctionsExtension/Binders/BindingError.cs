namespace AzureFunctionsExtension.Binders;

using System.ComponentModel.DataAnnotations;

public sealed class BindingError
{
    public BindingErrorKind Kind { get; }

    public BindingSource Source { get; }

    public string Name { get; }

    public IReadOnlyList<ValidationResult> ValidationResults { get; }

    public BindingError(BindingErrorKind kind, BindingSource source, string name, IReadOnlyList<ValidationResult>? validationResults = null)
    {
        Kind = kind;
        Source = source;
        Name = name;
        ValidationResults = validationResults ?? [];
    }
}
