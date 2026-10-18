using System.Runtime.CompilerServices;

using AzureFunctionsExtension.Annotations;

[assembly: CLSCompliant(false)]

[assembly: Filter<RequestIdFilter>]

[assembly: InternalsVisibleTo("AzureFunctionsExtension.Example.Tests")]
