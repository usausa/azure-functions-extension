using AzureFunctionsExtension;

using Microsoft.Azure.WebJobs.Hosting;

[assembly: CLSCompliant(false)]

[assembly: WebJobsStartup(typeof(BindingStartup))]
