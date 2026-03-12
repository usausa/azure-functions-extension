namespace AzureFunctionsExtension;

using System;

using AzureFunctionsExtension.Mvc;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureFunctionExtension(this IServiceCollection services)
    {
        return services.AddAzureFunctionExtension(static _ => { });
    }

    public static IServiceCollection AddAzureFunctionExtension(this IServiceCollection services, Action<JsonOptions> action)
    {
        services.Configure(action);
        services.AddSingleton<SystemTextJsonResultExecutor, SystemTextJsonResultExecutor>();
        return services;
    }
}
