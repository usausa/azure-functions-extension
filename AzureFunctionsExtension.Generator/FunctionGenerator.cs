namespace AzureFunctionsExtension.Generator;

using AzureFunctionsExtension.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerateHelper;

[Generator]
public sealed class FunctionGenerator : IIncrementalGenerator
{
    private const string AzureFunctionAttributeFullName = "AzureFunctionsExtension.Annotations.AzureFunctionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AzureFunctionAttributeFullName,
                static (syntax, _) => syntax is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (ctx, _) => FunctionModelBuilder.BuildFunctionModel(ctx));

        context.RegisterSourceOutput(provider, static (ctx, result) => ReportDiagnostics(ctx, result));
        context.RegisterImplementationSourceOutput(provider, static (ctx, result) => Execute(ctx, result));
    }

    private static void ReportDiagnostics(SourceProductionContext context, Result<FunctionModel> result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void Execute(SourceProductionContext context, Result<FunctionModel> result)
    {
        if (!result.HasValue)
        {
            return;
        }

        var model = result.Value;
        var builder = new SourceBuilder();

        FunctionSourceBuilder.BuildShared(builder, model);
        context.AddSource(HintNameBuilder.Build(model.Namespace, model.ClassName, "__shared__"), builder);

        foreach (var handler in model.Handlers)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            builder.Clear();
            FunctionSourceBuilder.Build(builder, model, handler);

            context.AddSource(HintNameBuilder.Build(model.Namespace, model.ClassName, handler.MethodName), builder);
        }
    }
}
