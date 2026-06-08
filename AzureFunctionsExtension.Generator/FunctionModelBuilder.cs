#pragma warning disable IDE0060, IDE0042, SA1313
namespace AzureFunctionsExtension.Generator;

using System.Globalization;
using System.Text.RegularExpressions;

using AzureFunctionsExtension.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

internal static class FunctionModelBuilder
{
    // ReSharper disable InconsistentNaming
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

    private const string HttpRequestFullName = "Microsoft.AspNetCore.Http.HttpRequest";
    private const string FunctionContextFullName = "Microsoft.Azure.Functions.Worker.FunctionContext";
    private const string IActionResultFullName = "Microsoft.AspNetCore.Mvc.IActionResult";

    private const string CancellationTokenFullName = "System.Threading.CancellationToken";
    // ReSharper restore InconsistentNaming

    // [AzureFunctionAttribute] が付与されたクラスから FunctionModel を構築するエントリーポイント。
    // Entry point: builds a FunctionModel from the class decorated with [AzureFunctionAttribute].
    public static Result<FunctionModel> BuildFunctionModel(GeneratorAttributeSyntaxContext context)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;

        // partial クラスでなければエラーを返す / Fail if the class is not partial
        var isPartial = syntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword));
        if (!isPartial)
        {
            return Results.Error<FunctionModel>(new DiagnosticInfo(
                Diagnostics.NotPartialClass, syntax.GetLocation(), symbol.Name));
        }

        // 生成コードは名前空間直下に partial 型として出力されるため、クラス定義の前提条件を検証する
        // The generated code is emitted as a partial type at namespace scope, so validate the class definition prerequisites
        if (symbol.IsGenericType)
        {
            return Results.Error<FunctionModel>(new DiagnosticInfo(
                Diagnostics.GenericClass, syntax.GetLocation(), symbol.Name));
        }

        if (symbol.ContainingType is not null)
        {
            return Results.Error<FunctionModel>(new DiagnosticInfo(
                Diagnostics.NestedClass, syntax.GetLocation(), symbol.Name));
        }

        if (symbol.IsRecord)
        {
            return Results.Error<FunctionModel>(new DiagnosticInfo(
                Diagnostics.RecordClass, syntax.GetLocation(), symbol.Name));
        }

        if (symbol.IsAbstract)
        {
            return Results.Error<FunctionModel>(new DiagnosticInfo(
                Diagnostics.AbstractClass, syntax.GetLocation(), symbol.Name));
        }

        // 名前空間・クラス型参照を取得する / Resolve namespace and type reference
        var ns = String.IsNullOrEmpty(symbol.ContainingNamespace.Name)
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        var functionType = MakeTypeRef(symbol);

        // Filter 属性を収集し、Order で昇順ソートしてパイプライン順序を確定する
        // Collect Filter attributes and sort by Order to determine pipeline sequence
        var filterAttrs = symbol.GetAttributes()
            .Select(static (a, i) => (Attr: a, Index: i))
            .Where(static x => IsFilterAttribute(x.Attr))
            .ToArray();

        var sortedFilters = filterAttrs
            .OrderBy(static x => GetFilterOrder(x.Attr))
            .ThenBy(static x => x.Index)
            .Select(static x => MakeTypeRef(x.Attr.AttributeClass!.TypeArguments[0]))
            .ToArray();

        var diagnostics = new List<DiagnosticInfo>();
        foreach (var filterAttr in filterAttrs)
        {
            var filterTypeArg = filterAttr.Attr.AttributeClass!.TypeArguments[0];
            if (filterTypeArg is INamedTypeSymbol filterTypeSym && !ImplementsInterface(filterTypeSym, IFunctionFilterFullName))
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.FilterNotImplementIFunctionFilter,
                    syntax.GetLocation(),
                    filterTypeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
        }

        if (diagnostics.Count > 0)
        {
            return Results.Error<FunctionModel>(diagnostics[0]);
        }

        // クラスの各メソッドを走査してハンドラーモデルを構築する
        // Iterate class members to build handler models
        var handlers = new List<HandlerModel>();
        var handlerNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if ((member.MethodKind != MethodKind.Ordinary) || member.IsStatic)
            {
                continue;
            }

            var handlerResult = BuildHandlerModel(member, diagnostics);
            if (handlerResult is null)
            {
                if (diagnostics.Count > 0)
                {
                    return Results.Error<FunctionModel>(diagnostics[0]);
                }

                continue;
            }

            // ハンドラー名 (= 生成される [Function] 名) は一意でなければならない。オーバーロードはエラー
            // Handler names (= generated [Function] names) must be unique; overloads are an error
            if (!handlerNames.Add(handlerResult.MethodName))
            {
                var loc = member.Locations.Length > 0 ? member.Locations[0] : null;
                return Results.Error<FunctionModel>(new DiagnosticInfo(
                    Diagnostics.OverloadedHandler, loc, handlerResult.MethodName));
            }

            handlers.Add(handlerResult);
        }

        return Results.Success(new FunctionModel(
            ns,
            symbol.Name,
            symbol.IsValueType,
            functionType,
            new EquatableArray<TypeRefModel>(sortedFilters),
            new EquatableArray<HandlerModel>(handlers.ToArray())));
    }

    private static bool IsFilterAttribute(AttributeData attr)
    {
        var attrClass = attr.AttributeClass;
        if (attrClass is null)
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

    // 戻り値の型が IActionResult そのもの、または IActionResult を実装しているかを判定する。
    // Determines whether the type is IActionResult itself or implements IActionResult.
    private static bool IsActionResult(ITypeSymbol type)
    {
        return (type.ToDisplayString() == IActionResultFullName) ||
               type.AllInterfaces.Any(static i => i.ToDisplayString() == IActionResultFullName);
    }

    // メソッドのエンドポイント属性 (Http/Timer/Queue) を解析してハンドラーの種類と設定を決定する。
    // Analyzes endpoint attributes (Http/Timer/Queue) on a method to determine handler handlerType and configuration.
    private static HandlerModel? BuildHandlerModel(IMethodSymbol method, List<DiagnosticInfo> diagnostics)
    {
        HandlerType? handlerType = null;
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
                handlerType = HandlerType.Http;
                httpMethod = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                route = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
                if (attr.ConstructorArguments.Length > 2)
                {
                    var levelValue = attr.ConstructorArguments[2].Value;
                    authorizationLevel = levelValue is not null ? GetAuthorizationLevelName((int)levelValue) : "Function";
                }
                else
                {
                    authorizationLevel = "Function";
                }
            }
            else if (attrName == TimerEndpointAttributeName)
            {
                handlerAttrCount++;
                handlerType = HandlerType.Timer;
                timerSchedule = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
            }
            else if (attrName == QueueEndpointAttributeName)
            {
                handlerAttrCount++;
                handlerType = HandlerType.Queue;
                queueName = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                queueConnection = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
            }
        }

        // エンドポイント属性がなければハンドラーではないのでスキップ
        // Skip if no endpoint attribute is present
        if (handlerType is null)
        {
            return null;
        }

        // 複数のエンドポイント属性が付いている場合はエラー
        // Error if multiple endpoint attributes are applied
        if (handlerAttrCount > 1)
        {
            var loc = method.Locations.Length > 0 ? method.Locations[0] : null;
            diagnostics.Add(new DiagnosticInfo(Diagnostics.MultipleHandlerAttributes, loc, method.Name));
            return null;
        }

        // 各パラメータのバインディング種別を解決してパラメータモデルを構築する
        // Resolve binding handlerType for each parameter and build parameter models
        var parameters = new List<ParameterModel>();
        foreach (var param in method.Parameters)
        {
            if (handlerType != HandlerType.Http)
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

            var paramModel = BuildParameterModel(param, handlerType.Value, diagnostics);
            if (paramModel is null)
            {
                return null;
            }

            parameters.Add(paramModel);
        }

        // Timer/Queue ハンドラーのトリガーペイロードは 1 つまで。複数あるとバインド先が曖昧になるためエラー
        // A Timer/Queue handler may bind at most one trigger payload; multiple would be ambiguous, so it is an error
        if (handlerType != HandlerType.Http)
        {
            var triggerCount = parameters.Count(static p => p.BindingType == ParameterBindingType.FromTrigger);
            if (triggerCount > 1)
            {
                var loc = method.Locations.Length > 0 ? method.Locations[0] : null;
                diagnostics.Add(new DiagnosticInfo(Diagnostics.MultipleTriggerPayloads, loc, method.Name));
                return null;
            }
        }

        if (handlerType == HandlerType.Http)
        {
            ValidateRouteBindings(route ?? method.Name, parameters, method, diagnostics);
            if (diagnostics.Count > 0)
            {
                return null;
            }
        }

        // 戻り値の型を解析して非同期かどうかと結果型を確定する
        // Analyze return type to determine async flag and actual result type
        var returnType = method.ReturnType;
        TypeRefModel? resultType;
        var isAsync = false;
        var resultIsActionResult = false;

        if (returnType is INamedTypeSymbol namedReturn)
        {
            if ((namedReturn.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>") ||
                (namedReturn.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>"))
            {
                isAsync = true;
                var inner = namedReturn.TypeArguments[0];
                resultType = MakeTypeRef(inner);
                resultIsActionResult = IsActionResult(inner);
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
                resultIsActionResult = IsActionResult(namedReturn);
            }
        }
        else
        {
            resultType = MakeTypeRef(returnType);
            resultIsActionResult = IsActionResult(returnType);
        }

        return new HandlerModel(
            method.Name,
            handlerType.Value,
            isAsync,
            resultType,
            resultIsActionResult,
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
            _ => "Function"
        };
    }

    // パラメータシンボルからバインディング属性を読み取り、ParameterModel を構築する。
    // Reads binding attributes from a parameter symbol and builds a ParameterModel.
    private static ParameterModel? BuildParameterModel(IParameterSymbol param, HandlerType handlerType, List<DiagnosticInfo> diagnostics)
    {
        var paramType = param.Type;
        var bindingType = ParameterBindingType.FromQuery;
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
                bindingType = ParameterBindingType.FromBody;
                var skipArg = attr.NamedArguments.FirstOrDefault(static a => a.Key == "SkipValidate").Value.Value;
                skipValidation = skipArg is true;
            }
            else if (attrName == FromQueryAttributeName)
            {
                bindingAttrCount++;
                bindingType = ParameterBindingType.FromQuery;
                var nameArg = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                if (!String.IsNullOrEmpty(nameArg))
                {
                    key = nameArg!;
                }
            }
            else if (attrName == FromHeaderAttributeName)
            {
                bindingAttrCount++;
                bindingType = ParameterBindingType.FromHeader;
                var nameArg = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                if (!String.IsNullOrEmpty(nameArg))
                {
                    key = nameArg!;
                }
            }
            else if (attrName == FromRouteAttributeName)
            {
                bindingAttrCount++;
                bindingType = ParameterBindingType.FromRoute;
                var nameArg = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                if (!String.IsNullOrEmpty(nameArg))
                {
                    key = nameArg!;
                }
            }
            else if (attrName == FromServicesAttributeName)
            {
                bindingAttrCount++;
                bindingType = ParameterBindingType.FromServices;
                // [FromServices] のキーは未指定時 empty とし、keyed service 解決の有無を区別する
                // For [FromServices] the key defaults to empty so keyed vs. non-keyed resolution can be distinguished
                key = string.Empty;
                var nameArg = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                if (!String.IsNullOrEmpty(nameArg))
                {
                    key = nameArg!;
                }
            }
            else if (attrName == FromTriggerAttributeName)
            {
                bindingAttrCount++;
                bindingType = ParameterBindingType.FromTrigger;
            }
        }

        if (bindingAttrCount > 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.MultipleBindingAttributes,
                param.Locations.FirstOrDefault(),
                param.ContainingSymbol.Name,
                param.Name));
            return null;
        }

        // バインディング属性がない場合は型名で特殊パラメータ (HttpRequest など) を自動判定する
        // No binding attribute: auto-detect special parameters (HttpRequest, FunctionContext, etc.) by type name
        if (bindingAttrCount == 0)
        {
            var typeName = paramType.ToDisplayString();
            if (typeName == HttpRequestFullName)
            {
                bindingType = ParameterBindingType.HttpRequest;
                converterMethod = string.Empty;
            }
            else if (typeName == FunctionContextFullName)
            {
                bindingType = ParameterBindingType.Context;
                converterMethod = string.Empty;
            }
            else if (typeName == CancellationTokenFullName)
            {
                bindingType = ParameterBindingType.CancellationToken;
                converterMethod = string.Empty;
            }
            else if ((paramType is INamedTypeSymbol namedType) &&
                     (namedType.OriginalDefinition.ToDisplayString() == "Microsoft.Extensions.Logging.ILogger<TCategoryName>"))
            {
                bindingType = ParameterBindingType.Logger;
                converterMethod = string.Empty;
            }
            else if (handlerType != HandlerType.Http)
            {
                bindingType = ParameterBindingType.FromTrigger;
                converterMethod = string.Empty;
            }
        }

        if (!IsSupportedBindingType(paramType, bindingType, converterMethod))
        {
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.UnsupportedBindingType,
                param.Locations.FirstOrDefault(),
                paramType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return null;
        }

        // 明示的なデフォルト値があれば文字列リテラルとして保持する
        // Preserve explicit default value as a string literal if present
        var hasDefault = param.HasExplicitDefaultValue;
        string? defaultValueLiteral = null;
        if (hasDefault)
        {
            defaultValueLiteral = FormatDefaultValue(param.ExplicitDefaultValue);
        }

        return new ParameterModel(
            param.Name,
            MakeTypeRef(paramType),
            bindingType,
            key,
            converterMethod,
            skipValidation,
            hasDefault,
            defaultValueLiteral);
    }

    private static bool IsSupportedBindingType(ITypeSymbol type, ParameterBindingType bindingType, string converterMethod)
    {
        return bindingType switch
        {
            ParameterBindingType.HttpRequest or
            ParameterBindingType.Context or
            ParameterBindingType.CancellationToken or
            ParameterBindingType.Logger or
            ParameterBindingType.FromServices or
            ParameterBindingType.FromTrigger or
            ParameterBindingType.FromBody => true,
            ParameterBindingType.FromQuery or
            ParameterBindingType.FromHeader or
            ParameterBindingType.FromRoute => IsSupportedTextBindingType(type, converterMethod),
            _ => false
        };
    }

    private static bool IsSupportedTextBindingType(ITypeSymbol type, string converterMethod)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsSupportedTextBindingType(arrayType.ElementType, GetConverterMethod(arrayType.ElementType));
        }

        if ((type is INamedTypeSymbol namedType) &&
            (namedType.OriginalDefinition.ToDisplayString() == "System.Nullable<T>"))
        {
            return IsSupportedTextBindingType(namedType.TypeArguments[0], GetConverterMethod(namedType.TypeArguments[0]));
        }

        return !String.IsNullOrEmpty(converterMethod) || (type.SpecialType == SpecialType.System_String);
    }

    private static void ValidateRouteBindings(string route, IEnumerable<ParameterModel> parameters, IMethodSymbol method, List<DiagnosticInfo> diagnostics)
    {
        if (String.IsNullOrWhiteSpace(route))
        {
            return;
        }

        var routeParameters = ExtractRouteParameterNames(route);
        if (routeParameters.Count == 0)
        {
            return;
        }

        var boundRouteParameters = parameters
            .Where(static p => p.BindingType == ParameterBindingType.FromRoute)
            .Select(static p => p.Key);
        var boundRouteParameterSet = new HashSet<string>(boundRouteParameters, StringComparer.OrdinalIgnoreCase);

        foreach (var routeParameter in routeParameters)
        {
            if (!boundRouteParameterSet.Contains(routeParameter))
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.MissingRouteParameter,
                    method.Locations.FirstOrDefault(),
                    routeParameter,
                    method.Name));
            }
        }
    }

    private static HashSet<string> ExtractRouteParameterNames(string route)
    {
        var parameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(route, "\\{([^{}]+)\\}"))
        {
            if (!match.Success)
            {
                continue;
            }

            var token = match.Groups[1].Value.Trim();
            token = token.TrimStart('*');

            var separatorIndex = token.IndexOfAny([':', '=', '?']);
            if (separatorIndex >= 0)
            {
                token = token.Substring(0, separatorIndex);
            }

            if (!String.IsNullOrWhiteSpace(token))
            {
                parameters.Add(token);
            }
        }

        return parameters;
    }

    private static string FormatDefaultValue(object? value)
    {
        if (value is null)
        {
            return "default";
        }

        if (value is string s)
        {
            return SymbolDisplay.FormatLiteral(s, quote: true);
        }

        if (value is bool b)
        {
            return b ? "true" : "false";
        }

        if (value is char c)
        {
            return SymbolDisplay.FormatLiteral(c, quote: true);
        }

        // 数値などはカルチャ非依存のリテラルとして出力する (型キャストは呼び出し側で付与)
        // Emit numeric and other literals using invariant culture (the cast is added by the caller).
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "default";
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
            _ => string.Empty
        };
    }

    // ITypeSymbol を TypeRefModel に変換する。配列・Nullable を考慮して再帰的に解決する。
    // Converts an ITypeSymbol to TypeRefModel, recursively handling arrays and Nullable<T>.
    private static TypeRefModel MakeTypeRef(ITypeSymbol type)
    {
        var isNullable = false;
        TypeRefModel? underlyingType = null;
        var isReferenceType = type.IsReferenceType;
        var isNullableReferenceType = isReferenceType && (type.NullableAnnotation == NullableAnnotation.Annotated);

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
                true,
                MakeTypeRef(arr.ElementType),
                false,
                null,
                true,
                false);
        }

        return new TypeRefModel(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            false,
            null,
            isNullable,
            underlyingType,
            isReferenceType,
            isNullableReferenceType);
    }
}
