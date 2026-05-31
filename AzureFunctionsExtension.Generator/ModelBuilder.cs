#pragma warning disable IDE0060, IDE0042, SA1313
namespace AzureFunctionsExtension.Generator;

using AzureFunctionsExtension.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

internal static class ModelBuilder
{
    private const string ServiceResolverAttributeName = "AzureFunctionsExtension.Annotations.ServiceResolverAttribute";
    private const string FilterAttributeName = "AzureFunctionsExtension.Annotations.FilterAttribute`1";
    private const string HttpEndpointAttributeName = "AzureFunctionsExtension.Annotations.HttpEndpointAttribute";
    private const string TimerEndpointAttributeName = "AzureFunctionsExtension.Annotations.TimerEndpointAttribute";
    private const string QueueEndpointAttributeName = "AzureFunctionsExtension.Annotations.QueueEndpointAttribute";

    private const string FromBodyAttributeName = "AzureFunctionsExtension.Annotations.FromBodyAttribute";
    private const string FromQueryAttributeName = "AzureFunctionsExtension.Annotations.FromQueryAttribute";
    private const string FromHeaderAttributeName = "AzureFunctionsExtension.Annotations.FromHeaderAttribute";
    private const string FromRouteAttributeName = "AzureFunctionsExtension.Annotations.FromRouteAttribute";
    private const string FromServicesAttributeName = "AzureFunctionsExtension.Annotations.FromServicesAttribute";
    private const string FromTriggerAttributeName = "AzureFunctionsExtension.Annotations.FromTriggerAttribute";

    private const string IFunctionFilterFullName = "AzureFunctionsExtension.Filters.IFunctionFilter";
    private const string IServiceCollectionFullName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private const string HttpRequestFullName = "Microsoft.AspNetCore.Http.HttpRequest";
    private const string FunctionContextFullName = "Microsoft.Azure.Functions.Worker.FunctionContext";
    private const string CancellationTokenFullName = "System.Threading.CancellationToken";

    public static Result<FunctionModel> BuildFunctionModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (ClassDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        var isPartial = syntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword));
        if (!isPartial)
        {
            return Results.Error<FunctionModel>(new DiagnosticInfo(
                Diagnostics.NotPartialClass, syntax.GetLocation(), symbol.Name));
        }

        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name)
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        var functionType = MakeTypeRef(symbol);

        var ctor = symbol.InstanceConstructors
            .Where(static c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(static c => c.Parameters.Length)
            .FirstOrDefault();

        var ctorParams = ctor != null
            ? ctor.Parameters.Select(static p => MakeTypeRef(p.Type)).ToArray()
            : [];

        ServiceResolverModel? serviceResolver = null;
        var serviceResolverAttr = symbol.GetAttributes()
            .FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == ServiceResolverAttributeName);
        if (serviceResolverAttr != null)
        {
            if (serviceResolverAttr.ConstructorArguments.Length > 0 &&
                serviceResolverAttr.ConstructorArguments[0].Value is INamedTypeSymbol resolverType)
            {
                var configureMethod = resolverType.GetMembers("ConfigureServices")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(static m => m.IsStatic && (m.DeclaredAccessibility == Accessibility.Public)
                        && (m.Parameters.Length == 0)
                        && (m.ReturnType.ToDisplayString() == IServiceCollectionFullName));

                if (configureMethod == null)
                {
                    return Results.Error<FunctionModel>(new DiagnosticInfo(
                        Diagnostics.InvalidServiceResolverType, syntax.GetLocation(), resolverType.ToDisplayString()));
                }

                serviceResolver = new ServiceResolverModel(MakeTypeRef(resolverType));
            }
        }
        else if (ctorParams.Length > 0)
        {
            return Results.Error<FunctionModel>(new DiagnosticInfo(
                Diagnostics.MissingServiceResolver, syntax.GetLocation(), symbol.Name));
        }

        var filterAttrs = symbol.GetAttributes()
            .Select(static (a, i) => (Attr: a, Index: i))
            .Where(static x => IsFilterAttribute(x.Attr))
            .ToArray();

        var sortedFilters = filterAttrs
            .OrderBy(static x => GetFilterOrder(x.Attr))
            .ThenBy(static x => x.Index)
            .Select((x, idx) =>
            {
                var filterType = x.Attr.AttributeClass!.TypeArguments[0];
                return new FilterDescriptorModel(idx, MakeTypeRef(filterType), GetFilterOrder(x.Attr));
            })
            .ToArray();

        var diagnostics = new List<DiagnosticInfo>();
        foreach (var fd in sortedFilters)
        {
            var filterAttr = filterAttrs.First(x => GetFilterOrder(x.Attr) == fd.Order);
            var filterTypeArg = filterAttr.Attr.AttributeClass!.TypeArguments[0];
            if (filterTypeArg is INamedTypeSymbol filterTypeSym && !ImplementsInterface(filterTypeSym, IFunctionFilterFullName))
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.FilterNotImplementIFunctionFilter, syntax.GetLocation(), fd.FilterType.FullName));
            }
        }

        if (diagnostics.Count > 0)
        {
            return Results.Error<FunctionModel>(diagnostics[0]);
        }

        var handlers = new List<HandlerModel>();
        foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if ((member.MethodKind != MethodKind.Ordinary) || member.IsStatic)
            {
                continue;
            }

            var handlerResult = BuildHandlerModel(member, diagnostics);
            if (handlerResult == null)
            {
                if (diagnostics.Count > 0)
                {
                    return Results.Error<FunctionModel>(diagnostics[0]);
                }

                continue;
            }

            handlers.Add(handlerResult);
        }

        return Results.Success(new FunctionModel(
            ns,
            symbol.Name,
            symbol.IsValueType,
            functionType,
            new EquatableArray<TypeRefModel>(ctorParams),
            serviceResolver,
            new EquatableArray<FilterDescriptorModel>(sortedFilters),
            new EquatableArray<HandlerModel>(handlers.ToArray())));
    }

    private static bool IsFilterAttribute(AttributeData attr)
    {
        var attrClass = attr.AttributeClass;
        if (attrClass == null)
        {
            return false;
        }

        if (attrClass.IsGenericType)
        {
            var original = attrClass.OriginalDefinition;
            var ns = original.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            return ns + "." + original.MetadataName == FilterAttributeName;
        }

        return false;
    }

    private static int GetFilterOrder(AttributeData attr)
    {
        var namedArg = attr.NamedArguments.FirstOrDefault(static a => a.Key == "Order");
        if (namedArg.Value.Value is int order)
        {
            return order;
        }

        return 0;
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceFullName)
    {
        return type.AllInterfaces.Any(i => i.ToDisplayString() == interfaceFullName);
    }

    private static HandlerModel? BuildHandlerModel(IMethodSymbol method, List<DiagnosticInfo> diagnostics)
    {
        HandlerKind? kind = null;
        string? httpMethod = null;
        string? route = null;
        string? authorizationLevel = null;
        string? timerSchedule = null;
        string? queueName = null;
        string? queueConnection = null;
        var handlerAttrCount = 0;

        foreach (var attr in method.GetAttributes())
        {
            var attrName = attr.AttributeClass?.ToDisplayString();
            if (attrName == HttpEndpointAttributeName)
            {
                handlerAttrCount++;
                kind = HandlerKind.Http;
                httpMethod = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                route = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
                if (attr.ConstructorArguments.Length > 2)
                {
                    var levelValue = attr.ConstructorArguments[2].Value;
                    authorizationLevel = levelValue != null ? GetAuthorizationLevelName((int)levelValue) : "Function";
                }
                else
                {
                    authorizationLevel = "Function";
                }
            }
            else if (attrName == TimerEndpointAttributeName)
            {
                handlerAttrCount++;
                kind = HandlerKind.Timer;
                timerSchedule = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
            }
            else if (attrName == QueueEndpointAttributeName)
            {
                handlerAttrCount++;
                kind = HandlerKind.Queue;
                queueName = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                queueConnection = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
            }
        }

        if (kind == null)
        {
            return null;
        }

        if (handlerAttrCount > 1)
        {
            var loc = method.Locations.Length > 0 ? method.Locations[0] : null;
            diagnostics.Add(new DiagnosticInfo(Diagnostics.MultipleHandlerAttributes, loc, method.Name));
            return null;
        }

        var parameters = new List<ParameterModel>();
        foreach (var param in method.Parameters)
        {
            if (kind != HandlerKind.Http)
            {
                var hasHttpOnlyAttr = param.GetAttributes().Any(a =>
                    (a.AttributeClass?.ToDisplayString() == FromQueryAttributeName) ||
                    (a.AttributeClass?.ToDisplayString() == FromRouteAttributeName) ||
                    (a.AttributeClass?.ToDisplayString() == FromHeaderAttributeName) ||
                    (a.AttributeClass?.ToDisplayString() == FromBodyAttributeName));
                if (hasHttpOnlyAttr)
                {
                    var loc = method.Locations.Length > 0 ? method.Locations[0] : null;
                    diagnostics.Add(new DiagnosticInfo(Diagnostics.InvalidBindingOnNonHttpHandler, loc, method.Name));
                    return null;
                }
            }

            var paramModel = BuildParameterModel(param, kind.Value);
            parameters.Add(paramModel);
        }

        var returnType = method.ReturnType;
        TypeRefModel? resultType;
        var isAsync = false;

        if (returnType is INamedTypeSymbol namedReturn)
        {
            if ((namedReturn.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>") ||
                (namedReturn.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>"))
            {
                isAsync = true;
                var inner = namedReturn.TypeArguments[0];
                resultType = MakeTypeRef(inner);
            }
            else if ((namedReturn.ToDisplayString() == "System.Threading.Tasks.Task") ||
                     (namedReturn.ToDisplayString() == "System.Threading.Tasks.ValueTask"))
            {
                isAsync = true;
                resultType = null;
            }
            else if (namedReturn.ToDisplayString() == "void")
            {
                resultType = null;
            }
            else
            {
                resultType = MakeTypeRef(namedReturn);
            }
        }
        else
        {
            resultType = MakeTypeRef(returnType);
        }

        return new HandlerModel(
            method.Name,
            kind.Value,
            isAsync,
            resultType,
            new EquatableArray<ParameterModel>(parameters.ToArray()),
            httpMethod,
            route,
            authorizationLevel,
            timerSchedule,
            queueName,
            queueConnection);
    }

    private static string GetAuthorizationLevelName(int value)
    {
        return value switch
        {
            0 => "Anonymous",
            1 => "User",
            2 => "Function",
            3 => "System",
            4 => "Admin",
            _ => "Function",
        };
    }

    private static ParameterModel BuildParameterModel(IParameterSymbol param, HandlerKind handlerKind)
    {
        var paramType = param.Type;
        var bindingKind = ParameterBindingKind.FromQuery;
        var key = param.Name;
        var converterMethod = GetConverterMethod(paramType);
        var skipValidation = false;

        var bindingAttrCount = 0;
        foreach (var attr in param.GetAttributes())
        {
            var attrName = attr.AttributeClass?.ToDisplayString();
            if (attrName == FromBodyAttributeName)
            {
                bindingAttrCount++;
                bindingKind = ParameterBindingKind.FromBody;
                var skipArg = attr.NamedArguments.FirstOrDefault(static a => a.Key == "SkipValidate").Value.Value;
                skipValidation = skipArg is true;
            }
            else if (attrName == FromQueryAttributeName)
            {
                bindingAttrCount++;
                bindingKind = ParameterBindingKind.FromQuery;
                var nameArg = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                if (!String.IsNullOrEmpty(nameArg))
                {
                    key = nameArg!;
                }
            }
            else if (attrName == FromHeaderAttributeName)
            {
                bindingAttrCount++;
                bindingKind = ParameterBindingKind.FromHeader;
                var nameArg = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                if (!String.IsNullOrEmpty(nameArg))
                {
                    key = nameArg!;
                }
            }
            else if (attrName == FromRouteAttributeName)
            {
                bindingAttrCount++;
                bindingKind = ParameterBindingKind.FromRoute;
                var nameArg = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                if (!String.IsNullOrEmpty(nameArg))
                {
                    key = nameArg!;
                }
            }
            else if (attrName == FromServicesAttributeName)
            {
                bindingAttrCount++;
                bindingKind = ParameterBindingKind.FromServices;
            }
            else if (attrName == FromTriggerAttributeName)
            {
                bindingAttrCount++;
                bindingKind = ParameterBindingKind.FromTrigger;
            }
        }

        if (bindingAttrCount == 0)
        {
            var typeName = paramType.ToDisplayString();
            if (typeName == HttpRequestFullName)
            {
                bindingKind = ParameterBindingKind.HttpRequest;
                converterMethod = string.Empty;
            }
            else if (typeName == FunctionContextFullName)
            {
                bindingKind = ParameterBindingKind.FunctionContext;
                converterMethod = string.Empty;
            }
            else if (typeName == CancellationTokenFullName)
            {
                bindingKind = ParameterBindingKind.CancellationToken;
                converterMethod = string.Empty;
            }
            else if ((paramType is INamedTypeSymbol namedType) &&
                     (namedType.OriginalDefinition.ToDisplayString() == "Microsoft.Extensions.Logging.ILogger<TCategoryName>"))
            {
                bindingKind = ParameterBindingKind.Logger;
                converterMethod = string.Empty;
            }
            else if (handlerKind != HandlerKind.Http)
            {
                bindingKind = ParameterBindingKind.FromTrigger;
                converterMethod = string.Empty;
            }
        }

        var hasDefault = param.HasExplicitDefaultValue;
        string? defaultValueLiteral = null;
        if (hasDefault)
        {
            defaultValueLiteral = FormatDefaultValue(param.ExplicitDefaultValue, paramType);
        }

        return new ParameterModel(
            param.Name,
            MakeTypeRef(paramType),
            bindingKind,
            key,
            converterMethod,
            skipValidation,
            hasDefault,
            defaultValueLiteral);
    }

    private static string FormatDefaultValue(object? value, ITypeSymbol type)
    {
        if (value == null)
        {
            return "default";
        }

        if (value is string s)
        {
            return $"\"{s}\"";
        }

        if (value is bool b)
        {
            return b ? "true" : "false";
        }

        if (value is char c)
        {
            return $"'{c}'";
        }

        return value.ToString() ?? "default";
    }

    private static string GetConverterMethod(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arr)
        {
            return GetConverterMethod(arr.ElementType);
        }

        if ((type is INamedTypeSymbol named) && (named.OriginalDefinition.ToDisplayString() == "System.Nullable<T>"))
        {
            return GetConverterMethod(named.TypeArguments[0]);
        }

        var fullName = type.ToDisplayString();
        return fullName switch
        {
            "bool" or "System.Boolean" => "TryToBoolean",
            "byte" or "System.Byte" => "TryToByte",
            "sbyte" or "System.SByte" => "TryToSByte",
            "short" or "System.Int16" => "TryToInt16",
            "ushort" or "System.UInt16" => "TryToUInt16",
            "int" or "System.Int32" => "TryToInt32",
            "uint" or "System.UInt32" => "TryToUInt32",
            "long" or "System.Int64" => "TryToInt64",
            "ulong" or "System.UInt64" => "TryToUInt64",
            "float" or "System.Single" => "TryToSingle",
            "double" or "System.Double" => "TryToDouble",
            "decimal" or "System.Decimal" => "TryToDecimal",
            "char" or "System.Char" => "TryToChar",
            "System.DateTime" => "TryToDateTime",
            "System.DateTimeOffset" => "TryToDateTimeOffset",
            "System.DateOnly" => "TryToDateOnly",
            "System.TimeOnly" => "TryToTimeOnly",
            "System.TimeSpan" => "TryToTimeSpan",
            "System.Guid" => "TryToGuid",
            "string" or "System.String" => string.Empty,
            _ when type.TypeKind == TypeKind.Enum => "TryToEnum",
            _ => string.Empty,
        };
    }

    internal static TypeRefModel MakeTypeRef(ITypeSymbol type)
    {
        var isNullable = false;
        TypeRefModel? underlyingType = null;

        if ((type is INamedTypeSymbol namedType) &&
            (namedType.OriginalDefinition.ToDisplayString() == "System.Nullable<T>"))
        {
            isNullable = true;
            underlyingType = MakeTypeRef(namedType.TypeArguments[0]);
        }

        if (type is IArrayTypeSymbol arr)
        {
            return new TypeRefModel(
                type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                false,
                null,
                true,
                MakeTypeRef(arr.ElementType));
        }

        return new TypeRefModel(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            isNullable,
            underlyingType,
            false,
            null);
    }
}
