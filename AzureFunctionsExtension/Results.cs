namespace AzureFunctionsExtension;

using AzureFunctionsExtension.Mvc;

using Microsoft.AspNetCore.Mvc;

public static class Results
{
    public static IActionResult Of<T>(T value)
    {
        if (value is null)
        {
            return new NotFoundResult();
        }

        return new SystemTextJsonResult(value);
    }
}
