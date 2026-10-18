namespace AzureFunctionsExtension.Binders;

using Microsoft.AspNetCore.Mvc;

public interface IBindingErrorResultFactory
{
    IActionResult Create(BindingError bindingError);
}
