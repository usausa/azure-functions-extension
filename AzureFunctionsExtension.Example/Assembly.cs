using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: CLSCompliant(false)]

[assembly: FunctionsStartup(typeof(AzureFunctionsExtension.Example.Startup))]
