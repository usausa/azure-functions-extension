namespace AzureFunctionsExtension;

using System;
using System.Diagnostics.CodeAnalysis;

using AzureFunctionsExtension.Serialization;
using AzureFunctionsExtension.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Default serializer registration keeps the existing API shape.")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050", Justification = "Default serializer registration keeps the existing API shape.")]
    public static IServiceCollection AddAzureFunctionExtension(this IServiceCollection services)
    {
        return services.AddAzureFunctionExtension(static _ => { });
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Default serializer registration keeps the existing API shape.")]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050", Justification = "Default serializer registration keeps the existing API shape.")]
    public static IServiceCollection AddAzureFunctionExtension(this IServiceCollection services, Action<JsonOptions> action)
    {
        services.Configure(action);
        services.TryAddSingleton<IBodySerializer>(_ => JsonBodySerializer.Default);
        services.TryAddSingleton<IRequestValidator, DataAnnotationsRequestValidator>();
        return services;
    }
}
