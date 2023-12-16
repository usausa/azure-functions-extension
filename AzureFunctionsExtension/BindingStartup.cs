namespace AzureFunctionsExtension;

using AzureFunctionsExtension.Bindings;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;

#pragma warning disable CA1812
internal sealed class BindingStartup : IWebJobsStartup
{
    public void Configure(IWebJobsBuilder builder)
    {
        builder.AddExtension<BindingConfiguration>();
    }
}
#pragma warning restore CA1812
