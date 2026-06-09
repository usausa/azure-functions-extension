using AzureFunctionsExtension;
using AzureFunctionsExtension.Example.Functions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

KeepExampleTypesAlive();

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(static services =>
    {
        services.AddAzureFunctionExtension();
        services.AddSingleton<IGreetingService, GreetingService>();
        services.AddTransient<HttpFunction>();
        services.AddTransient<QueueFunction>();
        services.AddTransient<FilterFunction>();
        services.AddTransient<LoggingFilter>();
    })
    .Build();

host.Run();

static void KeepExampleTypesAlive()
{
    _ = new GreetingService();
    _ = new LoggingFilter();
    _ = new HttpFunction(null!);
    _ = new QueueFunction(null!);
    _ = new FilterFunction(null!);
}
