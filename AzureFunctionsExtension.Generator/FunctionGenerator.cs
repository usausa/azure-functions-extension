namespace AzureFunctionsExtension.Generator;

using System.Collections.Immutable;

using AzureFunctionsExtension.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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
                static (syntax, _) => IsClassSyntax(syntax),
                static (ctx, _) => FunctionModelBuilder.BuildFunctionModel(ctx));
        var treeProvider = context.ForAttributeWithMetadataNameSyntaxTrees(
            AzureFunctionAttributeFullName,
            static (syntax, _) => IsClassSyntax(syntax));

        var collected = provider.Collect();
        context.RegisterSourceOutput(
            collected.Combine(treeProvider),
            static (ctx, input) => ctx.ReportDiagnostics(input.Left.SelectError().Concat(FindHintNameCollisions(input.Left).Values).Distinct(), input.Right));
        var models = collected.SelectMany(static (results, _) => SelectModels(results));
        context.RegisterImplementationSourceOutput(models, static (ctx, model) => Execute(ctx, model));

        var functionNameProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AzureFunctionAttributeFullName,
                static (syntax, _) => IsClassSyntax(syntax),
                static (ctx, _) => FunctionModelBuilder.BuildFunctionNames(ctx))
            .SelectMany(static (names, _) => names)
            .Collect();
        context.RegisterSourceOutput(functionNameProvider.Combine(treeProvider), static (ctx, input) => ReportDuplicateFunctionNames(ctx, input.Left, input.Right));

        var workerSourceGenProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => IsWorkerSourceGenEnabled(provider.GlobalOptions));
        context.RegisterSourceOutput(workerSourceGenProvider.Combine(treeProvider), static (ctx, input) => ReportWorkerSourceGen(ctx, input.Left, input.Right));
    }

    private static bool IsClassSyntax(SyntaxNode syntax) =>
        syntax is ClassDeclarationSyntax or RecordDeclarationSyntax;

    private static bool IsWorkerSourceGenEnabled(AnalyzerConfigOptions options) =>
        (IsTrue(options, "FunctionsEnableMetadataSourceGen") && IsTrue(options, "FunctionsAutoRegisterGeneratedMetadataProvider")) ||
        (IsTrue(options, "FunctionsEnableExecutorSourceGen") && IsTrue(options, "FunctionsAutoRegisterGeneratedFunctionsExecutor"));

    private static bool IsTrue(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue<bool>(key, out var value) && value;

    private static void ReportWorkerSourceGen(SourceProductionContext context, bool enabled, ImmutableArray<SyntaxTree> trees)
    {
        if (enabled && !trees.IsEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.WorkerSourceGenEnabled, Location.None));
        }
    }

    private static void ReportDuplicateFunctionNames(SourceProductionContext context, ImmutableArray<FunctionNameModel> names, ImmutableArray<SyntaxTree> trees)
    {
        foreach (var group in names.GroupBy(static x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var firsts = group
                .GroupBy(static x => x.TypeName, StringComparer.Ordinal)
                .Select(static x => x.First())
                .ToList();
            if (firsts.Count < 2)
            {
                continue;
            }

            foreach (var name in firsts)
            {
                context.ReportDiagnostic(new DiagnosticInfo(Diagnostics.OverloadedHandler, name.Location, name.Name), trees);
            }
        }
    }

    private static string GetTypeName(FunctionModel model) =>
        String.IsNullOrEmpty(model.Namespace) ? model.ClassName : $"{model.Namespace}.{model.ClassName}";

    private static string GetHintKey(FunctionModel model) =>
        HintNameBuilder.Build(model.Namespace, model.ClassName);

    private static IEnumerable<FunctionModel> SelectModels(ImmutableArray<Result<FunctionModel>> results)
    {
        var collisions = FindHintNameCollisions(results);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in results.SelectValue())
        {
            var key = GetHintKey(model);
            if (!collisions.ContainsKey(key) && emitted.Add(key))
            {
                yield return model;
            }
        }
    }

    private static Dictionary<string, DiagnosticInfo> FindHintNameCollisions(ImmutableArray<Result<FunctionModel>> results)
    {
        var collisions = new Dictionary<string, DiagnosticInfo>(StringComparer.Ordinal);
        var firsts = new Dictionary<string, FunctionModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in results.SelectValue().OrderBy(GetHintKey, StringComparer.Ordinal))
        {
            var key = GetHintKey(model);
            if (!firsts.TryGetValue(key, out var first))
            {
                firsts.Add(key, model);
            }
            else if ((GetHintKey(first) != key) && !collisions.ContainsKey(key))
            {
                collisions.Add(key, new DiagnosticInfo(Diagnostics.HintNameCollision, (Location?)null, GetTypeName(model), GetTypeName(first)));
            }
        }

        return collisions;
    }

    private static void Execute(SourceProductionContext context, FunctionModel model)
    {
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
