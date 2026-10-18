namespace AzureFunctionsExtension.Hosting;

using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal sealed class ExtensionStartupValidator : IHostedService
{
    private readonly IServiceProvider serviceProvider;

    public ExtensionStartupValidator(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var state = serviceProvider.GetService<ExtensionFunctionExecutorState>() ??
            throw new InvalidOperationException("AzureFunctionsExtension is not registered. Call AddAzureFunctionExtension().");

        _ = serviceProvider.GetRequiredService<IFunctionExecutor>();
        if (!state.Attached)
        {
            throw new InvalidOperationException("The function executor of AzureFunctionsExtension is not used. Call AddAzureFunctionExtension() after ConfigureFunctionsWebApplication().");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
