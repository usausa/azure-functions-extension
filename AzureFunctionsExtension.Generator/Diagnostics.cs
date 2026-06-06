namespace AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    // クラス宣言の検証 / Class declaration validation
    public static DiagnosticDescriptor NotPartialClass { get; } = new(
        id: "AFE0001",
        title: "[AzureFunction] class must be partial",
        messageFormat: "[AzureFunction] class '{0}' must be declared partial",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // クラスレベルの Filter 属性の検証 / Class-level filter attribute validation
    public static DiagnosticDescriptor FilterNotImplementIFunctionFilter { get; } = new(
        id: "AFE0002",
        title: "Filter type does not implement IFunctionFilter",
        messageFormat: "Filter type '{0}' does not implement IFunctionFilter",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ハンドラー (メソッド) 属性の検証 / Handler (method) attribute validation
    public static DiagnosticDescriptor MultipleHandlerAttributes { get; } = new(
        id: "AFE0003",
        title: "Handler has multiple handler attributes",
        messageFormat: "Handler '{0}' has multiple handler attributes",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // パラメータ・バインディングの検証 / Parameter binding validation
    public static DiagnosticDescriptor InvalidBindingOnNonHttpHandler { get; } = new(
        id: "AFE0004",
        title: "HTTP-only binding attribute used on non-HTTP handler",
        messageFormat: "HTTP-only binding attribute used on non-HTTP handler '{0}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MultipleBindingAttributes { get; } = new(
        id: "AFE0005",
        title: "Parameter has multiple binding attributes",
        messageFormat: "Parameter '{0}.{1}' has multiple binding attributes",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedBindingType { get; } = new(
        id: "AFE0006",
        title: "Unsupported parameter type for binding",
        messageFormat: "Parameter type '{0}' for binding is unsupported",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ルートテンプレートの検証 / Route template validation
    public static DiagnosticDescriptor MissingRouteParameter { get; } = new(
        id: "AFE0007",
        title: "Route template variable not bound with [FromRoute]",
        messageFormat: "Route template variable '{0}' in handler '{1}' is not bound with [FromRoute]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
