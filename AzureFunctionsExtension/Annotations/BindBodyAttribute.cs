namespace AzureFunctionsExtension.Annotations;

using Microsoft.Azure.WebJobs.Description;

[Binding]
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindBodyAttribute : Attribute
{
}
