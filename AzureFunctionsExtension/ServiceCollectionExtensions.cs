namespace AzureFunctionsExtension;

using System;

using AzureFunctionsExtension.Mvc;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSystemTextJsonResult(this IServiceCollection services)
    {
        return AddSystemTextJsonResult(services, _ => { });
    }

    public static IServiceCollection AddSystemTextJsonResult(this IServiceCollection services, Action<JsonOptions> action)
    {
        services.Configure(action);
        services.AddSingleton<SystemTextJsonResultExecutor, SystemTextJsonResultExecutor>();
        return services;
    }
}
