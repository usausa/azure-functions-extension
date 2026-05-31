namespace AzureFunctionsExtension.Generator;

using System.Collections.Immutable;
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
                static (syntax, _) => syntax is ClassDeclarationSyntax,
                static (ctx, _) => ModelBuilder.BuildFunctionModel(ctx))
            .Collect();

        context.RegisterImplementationSourceOutput(provider, static (ctx, results) => Execute(ctx, results));
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<FunctionModel>> results)
    {
        foreach (var diagnostic in results.SelectError())
        {
            context.ReportDiagnostic(diagnostic);
        }

        var builder = new SourceBuilder();

        foreach (var model in results.SelectValue())
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            builder.Clear();
            WrapperBuilder.BuildShared(builder, model);
            context.AddSource(
                MakeFilename(model.Namespace, model.ClassName, "__shared__"),
                SourceText.From(builder.ToString(), Encoding.UTF8));

            foreach (var handler in model.Handlers.AsArray())
            {
                builder.Clear();
                WrapperBuilder.Build(builder, model, handler);

                var filename = MakeFilename(model.Namespace, model.ClassName, handler.MethodName);
                context.AddSource(filename, SourceText.From(builder.ToString(), Encoding.UTF8));
            }
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
