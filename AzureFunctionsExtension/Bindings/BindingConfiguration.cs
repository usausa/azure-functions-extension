namespace AzureFunctionsExtension.Bindings;

using AzureFunctionsExtension;
using AzureFunctionsExtension.Annotations;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Options;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Framework only")]
internal sealed class BindingConfiguration : IExtensionConfigProvider
{
    private readonly BindingProvider bindingProvider;

    public BindingConfiguration(IHttpContextAccessor httpContextAccessor, IOptions<JsonOptions> options)
    {
        bindingProvider = new BindingProvider(httpContextAccessor, options.Value.Options);
    }

    public void Initialize(ExtensionConfigContext context)
    {
        context
            .AddBindingRule<BindBodyAttribute>()
            .Bind(bindingProvider);
        context
            .AddBindingRule<BindQueryAttribute>()
            .Bind(bindingProvider);
    }
}
