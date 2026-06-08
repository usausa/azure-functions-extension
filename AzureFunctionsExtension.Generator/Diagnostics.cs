namespace AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    // Class definition (AFE0001-AFE0005)
    public static DiagnosticDescriptor NotPartialClass { get; } = new(
        id: "AFE0001",
        title: "[AzureFunction] class must be partial",
        messageFormat: "[AzureFunction] class must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GenericClass { get; } = new(
        id: "AFE0002",
        title: "[AzureFunction] class must not be generic",
        messageFormat: "[AzureFunction] class must not be generic. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor NestedClass { get; } = new(
        id: "AFE0003",
        title: "[AzureFunction] class must be a top-level type",
        messageFormat: "[AzureFunction] class must be a top-level type. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor RecordClass { get; } = new(
        id: "AFE0004",
        title: "[AzureFunction] record is not supported",
        messageFormat: "[AzureFunction] record is not supported. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor AbstractClass { get; } = new(
        id: "AFE0005",
        title: "[AzureFunction] class must not be abstract",
        messageFormat: "[AzureFunction] class must not be abstract. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // Filter (AFE0006)
    public static DiagnosticDescriptor FilterNotImplementIFunctionFilter { get; } = new(
        id: "AFE0006",
        title: "Filter type does not implement IFunctionFilter",
        messageFormat: "Filter type does not implement IFunctionFilter. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // Function (AFE0007-AFE0010)
    public static DiagnosticDescriptor MultipleHandlerAttributes { get; } = new(
        id: "AFE0007",
        title: "Function has multiple endpoint attributes",
        messageFormat: "Function has multiple endpoint attributes. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor OverloadedHandler { get; } = new(
        id: "AFE0008",
        title: "Function is overloaded",
        messageFormat: "Function is overloaded; function names must be unique. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidBindingOnNonHttpHandler { get; } = new(
        id: "AFE0009",
        title: "HTTP-only binding attribute used on non-HTTP function",
        messageFormat: "HTTP-only binding attribute used on non-HTTP function. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor MultipleTriggerPayloads { get; } = new(
        id: "AFE0010",
        title: "Function has multiple trigger payload parameters",
        messageFormat: "Timer/Queue function has multiple trigger payload parameters. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // Parameter (AFE0011-AFE0012)
    public static DiagnosticDescriptor MultipleBindingAttributes { get; } = new(
        id: "AFE0011",
        title: "Parameter has multiple binding attributes",
        messageFormat: "Parameter has multiple binding attributes. function=[{0}], parameter=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UnsupportedBindingType { get; } = new(
        id: "AFE0012",
        title: "Unsupported parameter type for binding",
        messageFormat: "Parameter type for binding is unsupported. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // Route (AFE0013)
    public static DiagnosticDescriptor MissingRouteParameter { get; } = new(
        id: "AFE0013",
        title: "Route template variable not bound with [FromRoute]",
        messageFormat: "Route template variable is not bound with [FromRoute]. variable=[{0}], function=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
