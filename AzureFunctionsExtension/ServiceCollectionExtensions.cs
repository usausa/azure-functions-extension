namespace AzureFunctionsExtension;

using System;
using System.Diagnostics.CodeAnalysis;

using AzureFunctionsExtension.Hosting;
using AzureFunctionsExtension.Serialization;
using AzureFunctionsExtension.Validation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class ServiceCollectionExtensions
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Default serializer registration keeps the existing API shape.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Default serializer registration keeps the existing API shape.")]
    public static IServiceCollection AddAzureFunctionExtension(this IServiceCollection services)
    {
        services.AddOptions();
        services.TryAddSingleton<IBodySerializer>(static sp => new JsonBodySerializer(sp.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions));
        services.TryAddSingleton<IRequestValidator, DataAnnotationsRequestValidator>();
        AddFunctionCatalog(services);
        return services;
    }

    private static void AddFunctionCatalog(IServiceCollection services)
    {
        if (services.Any(static x => x.ServiceType == typeof(ExtensionFunctionExecutorState)))
        {
            return;
        }

        var previous = services.LastOrDefault(static x => (x.ServiceType == typeof(IFunctionExecutor)) && !x.IsKeyedService);
        services.AddSingleton(static _ => new ExtensionFunctionExecutorState());
        services.AddSingleton<IFunctionMetadataTransformer>(static _ => new ExtensionFunctionMetadataTransformer());
        services.AddSingleton<IFunctionExecutor>(sp => new ExtensionFunctionExecutor(
            previous is null ? null : ResolveExecutor(sp, previous),
            sp.GetRequiredService<ExtensionFunctionExecutorState>()));
    }

    private static IFunctionExecutor ResolveExecutor(IServiceProvider serviceProvider, ServiceDescriptor descriptor) =>
        (IFunctionExecutor)(descriptor.ImplementationInstance ??
            descriptor.ImplementationFactory?.Invoke(serviceProvider) ??
            ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!));
}
