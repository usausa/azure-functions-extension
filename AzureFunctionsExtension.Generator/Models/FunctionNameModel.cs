namespace AzureFunctionsExtension.Generator.Models;

using SourceGenerateHelper;

internal sealed record FunctionNameModel(
    string TypeName,
    string Name,
    LocationInfo? Location);
