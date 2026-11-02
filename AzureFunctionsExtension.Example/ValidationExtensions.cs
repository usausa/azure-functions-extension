namespace AzureFunctionsExtension.Example;

using AzureFunctionsExtension.Validation;

using Microsoft.Extensions.DependencyInjection;

internal static class ValidationExtensions
{
    public static IServiceCollection AddRequestValidation(this IServiceCollection services)
    {
        services.AddValidation();
        services.AddSingleton<IRequestValidator, ValidatableTypeRequestValidator>();
        return services;
    }
}
