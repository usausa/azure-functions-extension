namespace AzureFunctionsExtension.Generator;

using System.Text;

using AzureFunctionsExtension.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
        context.AddSource(
            MakeFilename(model.Namespace, model.ClassName, "__shared__"),
            SourceText.From(builder.ToString(), Encoding.UTF8));

        foreach (var handler in model.Handlers)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            builder.Clear();
            FunctionSourceBuilder.Build(builder, model, handler);

            var filename = MakeFilename(model.Namespace, model.ClassName, handler.MethodName);
            context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }

    private static string MakeFilename(string ns, string className, string methodName)
    {
        if (String.IsNullOrEmpty(ns))
        {
            return $"{className}__{methodName}.g.cs";
        }

        return $"{ns}.{className}__{methodName}.g.cs";
    }
}
