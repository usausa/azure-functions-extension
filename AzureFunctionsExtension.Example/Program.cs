using AzureFunctionsExtension;
using AzureFunctionsExtension.Binders;
using AzureFunctionsExtension.Example.Binders;
using AzureFunctionsExtension.Example.Functions;
using AzureFunctionsExtension.Example.Middleware;

using Microsoft.Azure.Functions.Worker.Builder;

KeepExampleTypesAlive();

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<ClientPrincipalMiddleware>();

builder.Services.AddAzureFunctionExtension();
builder.Services.AddSingleton<IBindingErrorResultFactory, BindingErrorResultFactory>();
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddTransient<LoggingFilter>();
builder.Services.AddTransient<RequestIdFilter>();

builder.Build().Run();

static void KeepExampleTypesAlive()
{
    _ = new GreetingService();
    _ = new LoggingFilter();
    _ = new RequestIdFilter();
    _ = new BindingErrorResultFactory();
    _ = new ClientPrincipalMiddleware();
    _ = new HttpFunction(null!);
    _ = new QueueFunction(null!);
    _ = new FilterFunction(null!);
    _ = new UserFunction(null!);
}
