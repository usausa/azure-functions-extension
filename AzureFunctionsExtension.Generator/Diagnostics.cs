namespace AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    public static DiagnosticDescriptor NotPartialClass { get; } = new(
        id: "AFE0001",
        title: "[AzureFunction] class must be partial",
        messageFormat: "[AzureFunction] class '{0}' must be declared partial",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor NoHandlerAttribute { get; } = new(
        id: "AFE0002",
        title: "Public method has no handler attribute",
        messageFormat: "Public method '{0}' has no handler attribute",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MultipleHandlerAttributes { get; } = new(
        id: "AFE0003",
        title: "Handler has multiple handler attributes",
        messageFormat: "Handler '{0}' has multiple handler attributes",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MultipleBindingAttributes { get; } = new(
        id: "AFE0004",
        title: "Parameter has multiple binding attributes",
        messageFormat: "Parameter '{0}.{1}' has multiple binding attributes",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidBindingOnNonHttpHandler { get; } = new(
        id: "AFE0005",
        title: "HTTP-only binding attribute used on non-HTTP handler",
        messageFormat: "HTTP-only binding attribute used on non-HTTP handler '{0}'",
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

    public static DiagnosticDescriptor MissingServiceResolver { get; } = new(
        id: "AFE0007",
        title: "[AzureFunction] class has constructor parameters but no [ServiceResolver]",
        messageFormat: "[AzureFunction] class '{0}' has constructor parameters but no [ServiceResolver]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidServiceResolverType { get; } = new(
        id: "AFE0008",
        title: "ServiceResolver type does not have ConfigureServices method",
        messageFormat: "ServiceResolver type '{0}' does not have a public static IServiceCollection ConfigureServices() method",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor FilterNotImplementIFunctionFilter { get; } = new(
        id: "AFE0009",
        title: "Filter type does not implement IFunctionFilter",
        messageFormat: "Filter type '{0}' does not implement IFunctionFilter",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MissingRouteParameter { get; } = new(
        id: "AFE0010",
        title: "Route template variable not bound with [FromRoute]",
        messageFormat: "Route template variable '{0}' in handler '{1}' is not bound with [FromRoute]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
