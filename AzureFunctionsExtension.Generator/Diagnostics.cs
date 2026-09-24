namespace AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper;

internal static class Diagnostics
{
    // Class definition (AFE0001-AFE0005)
    public static DiagnosticDescriptor NotPartialClass { get; } = new(
        id: "AFE0001",
        title: "Class must be partial",
        messageFormat: "[AzureFunction] class must be partial. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor GenericClass { get; } = new(
        id: "AFE0002",
        title: "Class must not be generic",
        messageFormat: "[AzureFunction] class must not be generic. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor NestedClass { get; } = new(
        id: "AFE0003",
        title: "Class must not be nested",
        messageFormat: "[AzureFunction] class must not be nested or file-local. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor RecordClass { get; } = new(
        id: "AFE0004",
        title: "Record is not supported",
        messageFormat: "[AzureFunction] record is not supported. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor AbstractClass { get; } = new(
        id: "AFE0005",
        title: "Class must not be abstract",
        messageFormat: "[AzureFunction] class must not be abstract. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // Filter (AFE0006)
    public static DiagnosticDescriptor FilterNotImplementIFunctionFilter { get; } = new(
        id: "AFE0006",
        title: "Invalid filter type",
        messageFormat: "Filter type does not implement IFunctionFilter. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // Function (AFE0007-AFE0010, AFE0014)
    public static DiagnosticDescriptor MultipleHandlerAttributes { get; } = new(
        id: "AFE0007",
        title: "Multiple endpoint attributes",
        messageFormat: "Function has multiple endpoint attributes. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor OverloadedHandler { get; } = new(
        id: "AFE0008",
        title: "Function is overloaded",
        messageFormat: "Function name is not unique. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor InvalidBindingOnNonHttpHandler { get; } = new(
        id: "AFE0009",
        title: "Invalid binding on non-HTTP function",
        messageFormat: "HTTP-only binding on a non-HTTP function. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor MultipleTriggerPayloads { get; } = new(
        id: "AFE0010",
        title: "Multiple trigger payloads",
        messageFormat: "[TimerEndpoint]/[QueueEndpoint] function has multiple payload parameters. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor GenericHandler { get; } = new(
        id: "AFE0014",
        title: "Function must not be generic",
        messageFormat: "Function must not be generic. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // Parameter (AFE0011-AFE0012)
    public static DiagnosticDescriptor MultipleBindingAttributes { get; } = new(
        id: "AFE0011",
        title: "Multiple binding attributes",
        messageFormat: "Parameter has multiple binding attributes. function=[{0}], parameter=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor UnsupportedBindingType { get; } = new(
        id: "AFE0012",
        title: "Unsupported binding type",
        messageFormat: "Binding type is not supported. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // Route (AFE0013)
    public static DiagnosticDescriptor MissingRouteParameter { get; } = new(
        id: "AFE0013",
        title: "Missing route parameter",
        messageFormat: "Route variable is not bound with [FromRoute]. variable=[{0}], function=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor WorkerSourceGenEnabled { get; } = new(
        id: "AFE0015",
        title: "Worker SDK source generation is enabled",
        messageFormat: "Functions generated by [AzureFunction] are not registered while the Worker SDK source generation is enabled. Set FunctionsEnableWorkerIndexing and FunctionsEnableExecutorSourceGen to false.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor UnsupportedTriggerPayload { get; } = new(
        id: "AFE0016",
        title: "Unsupported trigger payload",
        messageFormat: "Trigger payload must be TimerInfo for [TimerEndpoint] and string for [QueueEndpoint]. function=[{0}], parameter=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor BaseClassHandler { get; } = new(
        id: "AFE0017",
        title: "Handler declared in a base class",
        messageFormat: "Handler declared in a base class does not become a function. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ForeignBindingAttribute { get; } = new(
        id: "AFE0018",
        title: "Binding attribute of another library",
        messageFormat: "Attribute has the name of a binding attribute but is not AzureFunctionsExtension's, and is ignored. function=[{0}], parameter=[{1}], attribute=[{2}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor UndefinedEnumValue { get; } = new(
        id: "AFE0019",
        title: "Undefined enum value",
        messageFormat: "Attribute argument is not a defined enum value. function=[{0}], value=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor HintNameCollision { get; } = new(
        id: "AFE0020",
        title: "Type name differs only in case",
        messageFormat: "Type name differs only in case from another type, and its source is not generated. type=[{0}], other=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);
}
