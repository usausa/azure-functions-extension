namespace AzureFunctionsExtension;

using System;
using System.Diagnostics.CodeAnalysis;

using AzureFunctionsExtension.Serialization;
using AzureFunctionsExtension.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class ServiceCollectionExtensions
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Default serializer registration keeps the existing API shape.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Default serializer registration keeps the existing API shape.")]
    public static IServiceCollection AddAzureFunctionExtension(this IServiceCollection services)
    {
        return services.AddAzureFunctionExtension(static _ => { });
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Default serializer registration keeps the existing API shape.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Default serializer registration keeps the existing API shape.")]
    public static IServiceCollection AddAzureFunctionExtension(this IServiceCollection services, Action<JsonOptions> action)
    {
        services.Configure(action);
        services.TryAddSingleton<IBodySerializer>(static sp => new JsonBodySerializer(sp.GetRequiredService<IOptions<JsonOptions>>().Value.Options));
        services.TryAddSingleton<IRequestValidator, DataAnnotationsRequestValidator>();
        return services;
    }
}
