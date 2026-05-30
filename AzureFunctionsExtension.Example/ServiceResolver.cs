namespace AzureFunctionsExtension.Example;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class ServiceResolver
{
    public static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddAzureFunctionExtension();
        services.AddLogging(static b => b.AddConsole());
        return services;
    }
}
