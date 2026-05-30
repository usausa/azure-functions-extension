using AzureFunctionsExtension;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(static services => services.AddAzureFunctionExtension())
    .Build();

host.Run();
