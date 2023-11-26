namespace AzureFunctionsExtension;

using AzureFunctionsExtension.Bindings;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Framework only")]
internal sealed class BindingStartup : IWebJobsStartup
{
    public void Configure(IWebJobsBuilder builder)
    {
        builder.AddExtension<BindingConfiguration>();
    }
}
