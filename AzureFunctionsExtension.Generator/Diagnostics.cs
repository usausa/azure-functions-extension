namespace AzureFunctionsExtension.Generator;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper;

internal static class Diagnostics
{
    // Class definition (00xx)

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

    // Filter (01xx)

    public static DiagnosticDescriptor FilterNotImplementIFunctionFilter { get; } = new(
        id: "AFE0101",
        title: "Invalid filter type",
        messageFormat: "Filter type does not implement IFunctionFilter. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // Function (02xx)

    public static DiagnosticDescriptor UndefinedEnumValue { get; } = new(
        id: "AFE0201",
        title: "Undefined enum value",
        messageFormat: "Attribute argument is not a defined enum value. function=[{0}], value=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor MultipleHandlerAttributes { get; } = new(
        id: "AFE0202",
        title: "Multiple endpoint attributes",
        messageFormat: "Function has multiple endpoint attributes. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor GenericHandler { get; } = new(
        id: "AFE0203",
        title: "Function must not be generic",
        messageFormat: "Function must not be generic. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor MultipleTriggerPayloads { get; } = new(
        id: "AFE0204",
        title: "Multiple trigger payloads",
        messageFormat: "[TimerEndpoint]/[QueueEndpoint] function has multiple payload parameters. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor OverloadedHandler { get; } = new(
        id: "AFE0205",
        title: "Function is overloaded",
        messageFormat: "Function name is not unique. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor BaseClassHandler { get; } = new(
        id: "AFE0206",
        title: "Handler declared in a base class",
        messageFormat: "Handler declared in a base class does not become a function. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Parameter (03xx)

    public static DiagnosticDescriptor InvalidBindingOnNonHttpHandler { get; } = new(
        id: "AFE0301",
        title: "Invalid binding on non-HTTP function",
        messageFormat: "HTTP-only binding on a non-HTTP function. function=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor MultipleBindingAttributes { get; } = new(
        id: "AFE0302",
        title: "Multiple binding attributes",
        messageFormat: "Parameter has multiple binding attributes. function=[{0}], parameter=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor UnsupportedBindingType { get; } = new(
        id: "AFE0303",
        title: "Unsupported binding type",
        messageFormat: "Binding type is not supported. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor UnsupportedTriggerPayload { get; } = new(
        id: "AFE0304",
        title: "Unsupported trigger payload",
        messageFormat: "Trigger payload must be TimerInfo for [TimerEndpoint] and string for [QueueEndpoint]. function=[{0}], parameter=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor ForeignBindingAttribute { get; } = new(
        id: "AFE0305",
        title: "Binding attribute of another library",
        messageFormat: "Attribute has the name of a binding attribute but is not AzureFunctionsExtension's, and is ignored. function=[{0}], parameter=[{1}], attribute=[{2}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Route (04xx)

    public static DiagnosticDescriptor MissingRouteParameter { get; } = new(
        id: "AFE0401",
        title: "Missing route parameter",
        messageFormat: "Route variable is not bound with [FromRoute]. variable=[{0}], function=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Generation (05xx)

    public static DiagnosticDescriptor HintNameCollision { get; } = new(
        id: "AFE0501",
        title: "Type name differs only in case",
        messageFormat: "Type name differs only in case from another type, and its source is not generated. type=[{0}], other=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    // Build configuration (06xx)

    public static DiagnosticDescriptor AzureFunctionsSdkRequired { get; } = new(
        id: "AFE0601",
        title: "Azure.Functions.Sdk is required",
        messageFormat: "[AzureFunction] requires Azure.Functions.Sdk. Set the project SDK to Azure.Functions.Sdk.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor WorkerMetadataSourceGenDisabled { get; } = new(
        id: "AFE0602",
        title: "Worker SDK metadata source generation is disabled",
        messageFormat: "Functions generated by [AzureFunction] are not indexed while the Worker SDK metadata source generation is disabled. Remove the settings that disable FunctionsEnableMetadataSourceGen and FunctionsAutoRegisterGeneratedMetadataProvider.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);
}
