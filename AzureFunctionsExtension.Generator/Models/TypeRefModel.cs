namespace AzureFunctionsExtension.Generator.Models;

internal sealed record TypeRefModel(
    string FullName,
    bool IsArray,
    TypeRefModel? ElementType,
    bool IsNullable,
    TypeRefModel? UnderlyingType,
    bool IsReferenceType,
    bool IsNullableReferenceType);
