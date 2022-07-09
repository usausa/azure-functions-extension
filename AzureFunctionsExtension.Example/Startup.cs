namespace AzureFunctionsExtension.Example;

using Microsoft.Azure.Functions.Extensions.DependencyInjection;

public sealed class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddAzureFunctionExtension(c =>
        {
            c.Options.Converters.Add(new DateTimeConverter());
        });
    }
}
