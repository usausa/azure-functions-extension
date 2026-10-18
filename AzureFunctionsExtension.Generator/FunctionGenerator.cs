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
    private const string WorkerFunctionAttributeFullName = "Microsoft.Azure.Functions.Worker.FunctionAttribute";

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

        // スタブを生成するクラスと同じ集合からカタログを 1 ファイル生成する
        // Generate a single catalog file from the same set of classes as the stubs
        var assemblyNameProvider = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? string.Empty);
        var catalogProvider = collected
            .Select(static (results, _) => new EquatableArray<FunctionModel>(SelectModels(results).ToArray()))
            .Combine(assemblyNameProvider);
        context.RegisterImplementationSourceOutput(catalogProvider, static (ctx, input) => ExecuteCatalog(ctx, input.Left, input.Right));

        var functionNameProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AzureFunctionAttributeFullName,
                static (syntax, _) => IsClassSyntax(syntax),
                static (ctx, _) => FunctionModelBuilder.BuildFunctionNames(ctx))
            .SelectMany(static (names, _) => names)
            .Collect();

        // 手書きの [Function] も同じメタデータに合流するため、関数名の重複検出の対象にする (生成したスタブは入力に含まれない)
        // Hand-written [Function] methods join the same metadata, so they are included in the duplicate check (generated stubs are not in the input)
        var workerFunctionNameProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WorkerFunctionAttributeFullName,
                static (syntax, _) => syntax is MethodDeclarationSyntax,
                static (ctx, _) => ctx.Attributes.Length > 0 ? ctx.Attributes[0].ConstructorArguments.FirstOrDefault().Value as string : null)
            .Where(static x => x is not null)
            .Select(static (x, _) => x!)
            .Collect();
        context.RegisterSourceOutput(
            functionNameProvider.Combine(workerFunctionNameProvider).Combine(treeProvider),
            static (ctx, input) => ReportDuplicateFunctionNames(ctx, input.Left.Left, input.Left.Right, input.Right));

        var buildConfigProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => new BuildConfigModel(
                IsTrue(provider.GlobalOptions, "UsingAzureFunctionsSdk"),
                IsTrue(provider.GlobalOptions, "FunctionsEnableMetadataSourceGen") &&
                IsTrue(provider.GlobalOptions, "FunctionsAutoRegisterGeneratedMetadataProvider")));
        context.RegisterSourceOutput(buildConfigProvider.Combine(treeProvider), static (ctx, input) => ReportBuildConfiguration(ctx, input.Left, input.Right));
    }

    private static bool IsClassSyntax(SyntaxNode syntax) =>
        syntax is ClassDeclarationSyntax or RecordDeclarationSyntax;

    private static bool IsTrue(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue<bool>(key, out var value) && value;

    // 新SDK (Azure.Functions.Sdk) と、生成した関数のメタデータを追加するために必要なワーカーのメタデータ生成が有効かを検証する
    // Validates Azure.Functions.Sdk and the worker metadata generation required to add the metadata of the generated functions
    private static void ReportBuildConfiguration(SourceProductionContext context, BuildConfigModel config, ImmutableArray<SyntaxTree> trees)
    {
        if (trees.IsEmpty)
        {
            return;
        }

        if (!config.UsingAzureFunctionsSdk)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.AzureFunctionsSdkRequired, Location.None));
        }
        else if (!config.MetadataSourceGenEnabled)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.WorkerMetadataSourceGenDisabled, Location.None));
        }
    }

    private static void ReportDuplicateFunctionNames(SourceProductionContext context, ImmutableArray<FunctionNameModel> names, ImmutableArray<string> workerNames, ImmutableArray<SyntaxTree> trees)
    {
        var workerNameSet = new HashSet<string>(workerNames, StringComparer.OrdinalIgnoreCase);
        foreach (var group in names.GroupBy(static x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var firsts = group
                .GroupBy(static x => x.TypeName, StringComparer.Ordinal)
                .Select(static x => x.First())
                .ToList();
            if ((firsts.Count < 2) && !workerNameSet.Contains(group.Key))
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

    private static void ExecuteCatalog(SourceProductionContext context, EquatableArray<FunctionModel> models, string assemblyName)
    {
        if (!models.Any(static x => x.Handlers.Count > 0))
        {
            return;
        }

        var builder = new SourceBuilder();
        FunctionCatalogSourceBuilder.Build(builder, assemblyName, models);
        context.AddSource(FunctionCatalogSourceBuilder.HintName, builder);
    }
}
