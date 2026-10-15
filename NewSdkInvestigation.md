# Azure Functions 新SDK (`Azure.Functions.Sdk`) 対応調査

- 調査日: 2026-10-15
- 対象バージョン
  - `Azure.Functions.Sdk` 1.0.1 (2026-09-04 公開。1.0.0 GA は 2026-09-01)
  - `Microsoft.Azure.Functions.Worker` 2.52.0
  - `Microsoft.Azure.Functions.Worker.Sdk.Generators` 1.4.0 / `Microsoft.Azure.Functions.Worker.Sdk.Analyzers` 1.2.2 (新SDKが暗黙参照)
- 前提: 現行 SDK (`Microsoft.Azure.Functions.Worker.Sdk`) との互換性は考慮せず、新SDKのみを対象とする

---

## 1. 結論

1. 新SDKでは **ワーカーインデックスが常に有効** で、ビルド時の `functions.metadata` は **生成されない**。
   関数メタデータはワーカープロセス内の `IFunctionMetadataProvider` (Worker SDK のソースジェネレーターが生成) からホストへ返される。
2. 本ライブラリは「別のソースジェネレーターが生成した `[Function]` メソッドは Worker SDK のジェネレーターから見えない」問題を、
   **ワーカーインデックス無効化 + ビルド時 `functions.metadata` (Mono.Cecil でコンパイル済みアセンブリを走査)** で回避している。
   新SDKではこの前提が成り立たないため、**そのままでは動作しない** (実測: `AZFW0110` 警告 + `AFE0601` エラー。
   仮に `AFE0601` を外しても、関数 0 件のメタデータと何も実行しない Executor が登録される)。
3. 対応方針として、Worker の公開拡張ポイント **`IFunctionMetadataTransformer`** (Worker.Core 2.1.0 以降) で
   生成した関数のメタデータを追加し、**`IFunctionExecutor` のデコレーター** で生成した関数を直接呼び出すコードを
   本ライブラリのジェネレーターから出力する方式を推奨する。プロトタイプでワーカー側の動作 (メタデータ取得・実行ディスパッチ) を確認済み。
4. 利用者側は `Sdk="Azure.Functions.Sdk"` への切り替えと、`global.json` での SDK バージョン固定が必要
   (固定しないと並列ビルドで SDK 解決エラーになることを実測で確認)。

### 必要な変更の要約

| 区分 | 変更内容 |
| --- | --- |
| MSBuild (`AzureFunctionsExtension.props`) | `FunctionsEnableWorkerIndexing` / `FunctionsEnableExecutorSourceGen` の設定を削除。`UsingAzureFunctionsSdk` をジェネレーターから参照可能にする |
| ジェネレーター | 関数カタログ (メタデータ追加 + 実行ディスパッチ) の生成を追加。`AFE0601` を新SDK前提の診断に置き換え |
| ランタイムライブラリ | `IFunctionMetadataTransformer` 実装と `IFunctionExecutor` デコレーターを追加し、`AddAzureFunctionExtension()` で登録 |
| Example / テスト / README | 新SDK形式の csproj、`global.json`、`FunctionsApplication.CreateBuilder` 形式の Program.cs に更新。`AFE0601` テストを書き換え |

---

## 2. 新SDKの概要

### 2.1 位置付け

- `Microsoft.Azure.Functions.Worker.Sdk` (PackageReference で参照) を置き換える **MSBuild SDK**。`<Project Sdk="...">` で参照する。
- PackageReference で参照するとビルドエラーになる (`build/Azure.Functions.Sdk.targets` の `_AzureFunctionsIncorrectImport`)。
- 内部で `Microsoft.NET.Sdk.Worker` をインポートするため、`OutputType` は自動で `Exe` になる。
- `AzureFunctionsVersion` は既定で `v4` (`v4` 以外は `AZFW0106`)。サポート対象 TFM は `net8.0` 以上 (未満は `AZFW0107` 警告)。
- 次のパッケージを暗黙参照する。
  - `Microsoft.Azure.Functions.Worker.Sdk.Analyzers` 1.2.2
  - `Microsoft.Azure.Functions.Worker.Sdk.Generators` 1.4.0
- `Microsoft.Azure.Functions.Worker` は暗黙参照されない。復元後の `project.assets.json` に存在しない場合は `AZFW0111` 警告になり、
  Worker のソース生成も無効化される。推移的参照でも検出されるため、本ライブラリのパッケージ経由でも条件は満たす (タスク実装で確認)。
- `host.json` は出力・発行先へ、`local.settings.json` は出力先のみへ自動でコピーされる (csproj の `None Update` は不要。実測で確認)。
- `dotnet run` で Azure Functions Core Tools (`func start`) を起動する (`func` が無い場合は `AZFW0100`)。

### 2.2 プロジェクトファイル

```xml
<Project Sdk="Azure.Functions.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.1.1" />
    <PackageReference Include="AzureFunctionsExtension" Version="..." />
  </ItemGroup>

</Project>
```

`global.json` で SDK のバージョンを固定する。

```json
{
  "msbuild-sdks": {
    "Azure.Functions.Sdk": "1.0.1"
  }
}
```

> **注意:** `<Project Sdk="Azure.Functions.Sdk/1.0.1">` のように csproj 側だけでバージョン指定すると、
> 復元時に生成される `obj/azure_functions/azure_functions.g.csproj` (ホスト拡張の解決用プロジェクト) が
> **バージョン無しの** `<Project Sdk="Azure.Functions.Sdk">` を使うため、既定の並列ビルド (`-m`) で
> `MSB4236: The SDK 'Azure.Functions.Sdk' specified could not be found.` になる。
> `-m:1` では成功し、`global.json` で固定すると並列ビルドでも成功することを実測で確認した。

### 2.3 新SDKが出す診断 (`AZFW01xx`)

| ID | 種別 | 内容 |
| --- | --- | --- |
| AZFW0100 | Error | `dotnet run` で Core Tools を起動できない |
| AZFW0101 | Error | ホスト拡張パッケージのバージョン競合 |
| AZFW0102 | Warning | ホスト拡張パッケージの重複 |
| AZFW0103 | Error | ホスト拡張パッケージのバージョンが不正 |
| AZFW0104 | Warning | サポート終了した Functions バージョン |
| AZFW0105 | Error | `Microsoft.Azure.Functions.Worker.Sdk` / `Microsoft.NET.Sdk.Functions` を併用している |
| AZFW0106 | Error | 不明な `AzureFunctionsVersion` |
| AZFW0107 | Warning | サポート外の TFM |
| AZFW0108 | Warning | 拡張プロジェクトが復元前にビルドされた |
| AZFW0109 | Warning | 生成された `azure_functions.g.csproj` を直接ビルドした |
| AZFW0110 | Warning | `FunctionsEnableWorkerIndexing` は廃止 (常に有効) |
| AZFW0111 | Warning | `Microsoft.Azure.Functions.Worker` が参照されていない |

### 2.4 既知の制限 (パッケージ README より)

- `WebJobsStartupAttribute` の派生属性は認識されない (`WebJobsStartupAttribute` / `FunctionsStartupAttribute` の直接使用のみ)。
- プロジェクト参照 (P2P) の `ExtensionInformationAttribute` は使われない。

---

## 3. 新SDKの仕組み (本ライブラリに関係する部分)

### 3.1 強制される MSBuild プロパティ

`targets/Azure.Functions.Sdk.Analyzers.props` で **条件無し** に次の値が設定される。

| プロパティ | 値 |
| --- | --- |
| `FunctionsExecutionModel` | `isolated` |
| `FunctionsEnableMetadataSourceGen` | `true` |
| `FunctionsAutoRegisterGeneratedMetadataProvider` | `true` |
| `FunctionsEnableExecutorSourceGen` | `true` |
| `FunctionsAutoRegisterGeneratedFunctionsExecutor` | `true` |

- NuGet パッケージの `build/*.props` は SDK の `Sdk.props` 内 (`Microsoft.Common.props` → `nuget.g.props`) で先に読み込まれるため、
  **パッケージの `.props` で設定した値は上書きされる**。上書きしたい場合はパッケージの `.targets` かプロジェクト本体で設定する必要がある。
- `_ConfigureWorkerSourceGen` ターゲットは、Worker パッケージが見つからない場合に上記を `false` にするだけで、`true` に戻すことはない。
- `FunctionsEnableWorkerIndexing` を設定すると (値に関係なく) `AZFW0110` 警告になる。

### 3.2 生成物

| 生成物 | 新SDK |
| --- | --- |
| `functions.metadata` | **生成されない** (SDK に関数メタデータ生成タスクが存在しない) |
| `worker.config.json` | `"workerIndexing": "true"` 固定 |
| `extensions.json` / `.azurefunctions/` | `azure_functions.g.csproj` (net8.0) を復元して生成 |

### 3.3 実行時の流れ

```
Functions Host
  │ FunctionsMetadataRequest
  ▼
GrpcWorker.GetFunctionMetadataAsync
  └─ IFunctionMetadataManager (DefaultFunctionMetadataManager)
       ├─ IFunctionMetadataProvider.GetFunctionMetadataAsync(dir)
       │    └─ GeneratedFunctionMetadataProvider  ← Worker SDK のジェネレーターが生成・自動登録
       └─ IEnumerable<IFunctionMetadataTransformer>.Transform(list)  ← 公開拡張ポイント
  │
  │ FunctionLoadRequest (EntryPoint, ScriptFile, Bindings)
  ▼
GrpcFunctionDefinition
  └─ IMethodInfoLocator.GetMethod(PathToAssembly, EntryPoint).GetParameters()  ← エントリーポイントはリフレクションで解決
  │
  │ InvocationRequest
  ▼
ミドルウェア → FunctionExecutionMiddleware → IFunctionExecutor.ExecuteAsync(context)
                                              └─ DirectFunctionExecutor  ← Worker SDK のジェネレーターが生成・自動登録
```

ポイント:

- メタデータ (関数名・EntryPoint・バインディング JSON) はワーカーが返す。ホストによるアセンブリ走査は行われない。
- `IFunctionMetadataProvider` を Worker の既定実装 (`DefaultFunctionMetadataProvider`) にすると `functions.metadata` を読みに行き、
  新SDKではファイルが存在しないため `FileNotFoundException` になる (実測)。
- `IFunctionMetadataTransformer` は Worker.Core **2.1.0 で追加** された公開インターフェイス (2.0.0 には無い)。DI に複数登録でき、
  プロバイダーの結果に対して追加・変更・削除ができる。
- エントリーポイントのメソッドは実在する `public` メソッドである必要がある (パラメーター情報と `InputConverterAttribute` をリフレクションで取得するため)。
  バインディング名はエントリーポイントのパラメーター名と一致させる必要がある。

### 3.4 ソースジェネレーターが他のジェネレーターの出力を見られない問題

Roslyn のソースジェネレーターは全て同じ入力コンパイルに対して並列に実行され、互いの出力は見えない。
Worker SDK のジェネレーターは利用者が手書きした `[Function]` しか検出できず、本ライブラリが生成する `[Function]` は対象外になる。

`[AzureFunction]` クラスだけを持つ Example を新SDKでビルドした際に Worker SDK が生成したコード (抜粋):

```csharp
public class GeneratedFunctionMetadataProvider : IFunctionMetadataProvider
{
    public Task<ImmutableArray<IFunctionMetadata>> GetFunctionMetadataAsync(string directory)
    {
        var metadataList = new List<IFunctionMetadata>();

        return global::System.Threading.Tasks.Task.FromResult(metadataList.ToImmutableArray());
    }
}

internal class DirectFunctionExecutor : global::Microsoft.Azure.Functions.Worker.Invocation.IFunctionExecutor
{
    public async global::System.Threading.Tasks.ValueTask ExecuteAsync(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        var inputBindingFeature = context.Features.Get<global::Microsoft.Azure.Functions.Worker.Context.Features.IFunctionInputBindingFeature>();
        var inputBindingResult = await inputBindingFeature.BindFunctionInputAsync(context);
        var inputArguments = inputBindingResult.Values;

    }
}
```

- メタデータは 0 件 → ホストから見える関数が無い。
- Executor は未知の EntryPoint に対して **例外を出さず何もしない** (プロトタイプで確認)。メタデータだけ追加しても関数本体は実行されない。

### 3.5 自動登録の順序

Worker SDK が生成する `IAutoConfigureStartup` 実装は、`ConfigureFunctionsWorkerDefaults()` の中で
`Assembly.GetEntryAssembly().GetTypes()` から探索され、`ConfigureServices` コールバックとして登録される。

| ホスト構築方法 | 自動登録のタイミング | 利用者の `AddAzureFunctionExtension()` との順序 |
| --- | --- | --- |
| `FunctionsApplication.CreateBuilder(args)` | `CreateBuilder` 内で即時実行 | 必ず後 |
| `new HostBuilder().ConfigureFunctionsWebApplication().ConfigureServices(...)` | `Build()` 時にコールバック順で実行 | `ConfigureServices` を `ConfigureFunctionsWebApplication` より後に書けば後 |

いずれの方式でも、通常の書き方であれば `AddAzureFunctionExtension()` の登録が Worker SDK の自動登録より後になる (プロトタイプで両方式を確認)。

---

## 4. 現行実装への影響 (実測結果)

Example プロジェクトを新SDK形式に変更してビルド・実行した結果。

| 状態 | 結果 |
| --- | --- |
| csproj を新SDKに変更しただけ | `AZFW0110` 警告 (`AzureFunctionsExtension.props` の `FunctionsEnableWorkerIndexing`) + `AFE0601` エラー |
| `AFE0601` の判定を外した場合 | ビルドは成功するが、メタデータ 0 件・Executor は何もしない → 関数が動かない |
| メタデータのソース生成を無効化した場合 | 起動時に `FileNotFoundException: Function metadata file not found.` |
| 現行の `[Function]` スタブ (`Xxx_Handler`) | ビルド・直接呼び出しは問題なし (Example.Tests 15 件成功) |

現行で前提にしていた仕組みと新SDKでの状態:

| 現行の前提 | 新SDK |
| --- | --- |
| `FunctionsEnableWorkerIndexing=false` でホストインデックスにする | 廃止 (常にワーカーインデックス、設定すると `AZFW0110`) |
| SDK がビルド後にアセンブリを走査して `functions.metadata` を生成 | 生成されない |
| `FunctionsEnableExecutorSourceGen=false` で既定 Executor (リフレクション) を使う | SDK が `true` に上書きし、空の `DirectFunctionExecutor` が登録される |
| `AFE0601` でワーカーのソース生成が有効なら拒否 | 新SDKでは常に有効なので常にエラー |

---

## 5. 対応方針 (推奨案)

### 5.1 全体像

Worker SDK のジェネレーターの出力 (手書き `[Function]` 用) はそのまま活かし、本ライブラリが生成した関数を
**メタデータは Transformer で追加**、**実行は Executor のデコレーターで横取り** する。

```
IFunctionMetadataManager
  ├─ GeneratedFunctionMetadataProvider (Worker SDK 生成: 手書き [Function] 分)
  └─ ExtensionFunctionMetadataTransformer (本ライブラリ) ── 生成カタログから [AzureFunction] 分を追加

IFunctionExecutor
  └─ ExtensionFunctionExecutor (本ライブラリ・デコレーター)
       ├─ 生成カタログの EntryPoint → 生成コードで直接呼び出し
       └─ それ以外 → 元の Executor (Worker SDK 生成の DirectFunctionExecutor) へ委譲
```

- メタデータ・実行の両方をジェネレーターが静的に出力するため、リフレクションに依存しない (Worker SDK の生成コードと同等)。
- 手書きの `[Function]` との混在、他ライブラリの Transformer との共存が可能。
- 既存の `[Function]` スタブ (`Xxx_Handler`) は **エントリーポイントとして残す**
  (`GrpcFunctionDefinition` がリフレクションでパラメーターを取得するため。トリガー属性も `InputConverterAttribute` 解決に使われ得るので残す)。

### 5.2 ジェネレーターが追加で出力するコード (イメージ)

アプリケーションのアセンブリに 1 ファイル出力する。

```csharp
// <auto-generated/>
#nullable enable

namespace AzureFunctionsExtension.Generated;

internal sealed class FunctionCatalog : global::AzureFunctionsExtension.Hosting.IGeneratedFunctionCatalog
{
    [global::System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize() =>
        global::AzureFunctionsExtension.Hosting.GeneratedFunctionCatalog.Register(new FunctionCatalog());

    public void AddMetadata(global::System.Collections.Generic.IList<global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> metadata)
    {
        metadata.Add(new global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.DefaultFunctionMetadata
        {
            Language = "dotnet-isolated",
            Name = "Hello",
            EntryPoint = "MyApp.SampleFunction.Hello_Handler",
            ScriptFile = "MyApp.dll",
            RawBindings = new global::System.Collections.Generic.List<string>
            {
                @"{""name"":""req"",""type"":""httpTrigger"",""direction"":""In"",""authLevel"":""Anonymous"",""methods"":[""get""],""route"":""hello""}",
                @"{""name"":""$return"",""type"":""http"",""direction"":""Out""}",
            },
        });
    }

    public async global::System.Threading.Tasks.ValueTask<bool> TryExecuteAsync(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        switch (context.FunctionDefinition.EntryPoint)
        {
            case "MyApp.SampleFunction.Hello_Handler":
            {
                var input = await context.Features.Get<global::Microsoft.Azure.Functions.Worker.Context.Features.IFunctionInputBindingFeature>()!
                    .BindFunctionInputAsync(context);
                context.GetInvocationResult().Value = await global::MyApp.SampleFunction.Hello_Handler(
                    (global::Microsoft.AspNetCore.Http.HttpRequest)input.Values[0]!,
                    context);
                return true;
            }
        }

        return false;
    }
}
```

- `ScriptFile` は `Compilation.AssemblyName + ".dll"` (Worker SDK の生成コードと同じ規則)。
- `EntryPoint` は `名前空間.クラス名.メソッド名` (既存スタブ `Xxx_Handler`)。
- 呼び出し部分は Worker SDK の `DirectFunctionExecutor` と同じ形 (`BindFunctionInputAsync` → スタブ呼び出し → `GetInvocationResult().Value`)。
- JSON 文字列は `netstandard2.0` のジェネレーター内で組み立てるため、ルートやキュー名のエスケープ処理を実装する。
- 利用者の `LangVersion` に依存しないよう、コレクション式や raw 文字列リテラルは使わず、Worker SDK の生成コードと同じく逐語的文字列で出力する。

### 5.3 バインディング JSON

Worker SDK のジェネレーターが同じシグネチャの手書き関数に出力した JSON を基準にする (実測)。

| トリガー | `RawBindings` |
| --- | --- |
| HTTP | `{"name":"req","type":"httpTrigger","direction":"In","authLevel":"<AuthorizationLevel>","methods":["<method>"],"route":"<route>"}`<br>`{"name":"$return","type":"http","direction":"Out"}` |
| Timer | `{"name":"timerInfo","type":"timerTrigger","direction":"In","schedule":"<NCRONTAB>"}` |
| Queue | `{"name":"message","type":"queueTrigger","direction":"In","queueName":"<queue>","connection":"<connection>","dataType":"String"}` (`connection` は指定時のみ) |

- `name` は既存スタブのパラメーター名 (`req` / `timerInfo` / `message`) と一致させる。
- `HandlerModel` に必要な情報 (メソッド、ルート、認可レベル、スケジュール、キュー名、接続名) は既に揃っている。

### 5.4 ランタイムライブラリ側

```csharp
namespace AzureFunctionsExtension.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedFunctionCatalog
{
    void AddMetadata(IList<IFunctionMetadata> metadata);

    ValueTask<bool> TryExecuteAsync(FunctionContext context);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedFunctionCatalog
{
    public static void Register(IGeneratedFunctionCatalog catalog) { ... }

    internal static IReadOnlyList<IGeneratedFunctionCatalog> Catalogs { get; }
}

internal sealed class ExtensionFunctionMetadataTransformer : IFunctionMetadataTransformer
{
    public string Name => "AzureFunctionsExtension";

    public void Transform(IList<IFunctionMetadata> original)
    {
        foreach (var catalog in GeneratedFunctionCatalog.Catalogs)
        {
            catalog.AddMetadata(original);
        }
    }
}

internal sealed class ExtensionFunctionExecutor : IFunctionExecutor
{
    private readonly IFunctionExecutor? inner;

    public ExtensionFunctionExecutor(IFunctionExecutor? inner)
    {
        this.inner = inner;
    }

    public async ValueTask ExecuteAsync(FunctionContext context)
    {
        foreach (var catalog in GeneratedFunctionCatalog.Catalogs)
        {
            if (await catalog.TryExecuteAsync(context))
            {
                return;
            }
        }

        await inner!.ExecuteAsync(context);
    }
}
```

`AddAzureFunctionExtension()` での登録:

```csharp
services.AddSingleton<IFunctionMetadataTransformer, ExtensionFunctionMetadataTransformer>();

// 直前の IFunctionExecutor 登録 (Worker SDK 生成の DirectFunctionExecutor) を内側に包む
var previous = services.LastOrDefault(static x => x.ServiceType == typeof(IFunctionExecutor));
services.AddSingleton<IFunctionExecutor>(sp => new ExtensionFunctionExecutor(previous is null ? null : Resolve(sp, previous)));

static IFunctionExecutor Resolve(IServiceProvider sp, ServiceDescriptor descriptor) =>
    (IFunctionExecutor)(descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(sp)
        ?? ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
```

- `ServiceDescriptor.ImplementationType` は `DynamicallyAccessedMembers(PublicConstructors)` 注釈付きのため、トリミング解析上も問題になりにくい。
- 登録順序が逆 (`AddAzureFunctionExtension()` が Worker の自動登録より前) になると、デコレーターが上書きされて関数が無言で何もしなくなる。
  Transformer の中で解決済みの `IFunctionExecutor` が `ExtensionFunctionExecutor` であることを検証し、違えば例外にして起動時に検出させる。
- 依存する Worker の最低バージョンは 2.1.0 (`IFunctionMetadataTransformer`)。現行の参照 (2.52.0) で満たしている。

### 5.5 生成コードの登録方法の比較

| 方法 | 長所 | 短所 | 評価 |
| --- | --- | --- | --- |
| `[ModuleInitializer]` で静的レジストリへ登録し、`AddAzureFunctionExtension()` で DI 登録 | 利用者の追加手順が不要。リフレクション不要。登録順序が `AddAzureFunctionExtension()` の位置で決まる | 静的状態を持つ (テストで複数アセンブリを扱う場合は注意) | **推奨** |
| 生成コードに `IAutoConfigureStartup` 実装を出力 | Worker SDK と同じ仕組み | `GetTypes()` の列挙順に依存し、Worker SDK の Executor 登録との前後関係が保証されない | 非推奨 |
| 生成した拡張メソッド (例: `AddGeneratedFunctions()`) を利用者に呼ばせる | 明示的 | 呼び忘れると関数が 0 件になる。手順が増える | 次点 |

### 5.6 MSBuild (`AzureFunctionsExtension.props` / `.targets`)

- 削除: `FunctionsEnableWorkerIndexing` (`AZFW0110` の原因)、`FunctionsEnableExecutorSourceGen` (SDK に上書きされ効果が無い)。
- 追加: `<CompilerVisibleProperty Include="UsingAzureFunctionsSdk" />` (`Sdk.props` で `true` が設定される)。
  `FunctionsEnableMetadataSourceGen` などは SDK 側で既に `CompilerVisibleProperty` に登録済み。
- Worker の Executor ソース生成は有効のままでよい (デコレーターで包むため)。

### 5.7 診断の見直し

| ID | 変更 | 内容 |
| --- | --- | --- |
| AFE0601 | 置き換え | `build_property.UsingAzureFunctionsSdk` が `true` でない (新SDKを使っていない) 場合にエラー |
| AFE0602 | 追加 | `FunctionsEnableMetadataSourceGen` / `FunctionsAutoRegisterGeneratedMetadataProvider` が無効 (既定プロバイダーが `functions.metadata` を要求して起動に失敗する) 場合にエラー |
| AFE0205 | 拡張 (任意) | 手書きの `[Function]` と関数名が重複する場合も検出する (メタデータが同じリストに合流するため) |

---

## 6. 検討した代替案

| 案 | 内容 | 不採用の理由 |
| --- | --- | --- |
| A | Worker のメタデータ生成を無効化し、本ライブラリの MSBuild タスクで `functions.metadata` を生成する | Mono.Cecil などでアセンブリを走査するタスクの実装・配布が必要。旧SDKの仕組みの再実装になる |
| B | 利用者に `[Function]` + トリガー属性付きの `partial` メソッド宣言を書かせ、本体をジェネレーターで実装する | Worker SDK のジェネレーターから見えるようになるが、属性ベースの簡潔な記述というライブラリの価値が失われる |
| C | `IFunctionMetadataProvider` を丸ごと置き換える | 手書きの `[Function]` や他ライブラリのメタデータが失われる。公開拡張ポイントの Transformer で足りる |
| D | Executor ではなくミドルウェアで実行を横取りする | 利用者のミドルウェアとの前後関係に依存し、後から登録されたミドルウェアが適用されない |
| E | Executor のソース生成を `.targets` で無効化し、Worker 既定のリフレクション Executor に任せる | 動作はするが、リフレクション呼び出しになり AOT/トリミングに不利。メタデータ側の対応は別途必要 |

---

## 7. 変更対象ファイル

| ファイル | 変更内容 |
| --- | --- |
| `AzureFunctionsExtension.props` | 5.6 の通り |
| `AzureFunctionsExtension.Generator/FunctionGenerator.cs` | 旧 `AFE0601` 判定を削除し 5.7 の判定を追加。全モデルを集約してカタログを 1 ファイル出力 |
| `AzureFunctionsExtension.Generator/FunctionSourceBuilder.cs` (または新規ビルダー) | カタログ (メタデータ JSON + ディスパッチ) の出力 |
| `AzureFunctionsExtension.Generator/Diagnostics.cs` / `Diagnostics.md` | 5.7 の通り |
| `AzureFunctionsExtension/` | `Hosting` 名前空間 (レジストリ、Transformer、Executor デコレーター)、`ServiceCollectionExtensions` の登録追加 |
| `AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj` | `Sdk="Azure.Functions.Sdk"`。`AzureFunctionsVersion` / `OutputType` / `Microsoft.Azure.Functions.Worker.Sdk` / `host.json` 等のコピー指定を削除 |
| `AzureFunctionsExtension.Example/Program.cs` | `FunctionsApplication.CreateBuilder(args)` 形式に変更 |
| `global.json` (新規) | `msbuild-sdks` で `Azure.Functions.Sdk` を固定 |
| `AzureFunctionsExtension.Tests/DiagnosticTests.cs` | `AFE0601` テストを新条件に書き換え、`AFE0602` を追加 |
| `AzureFunctionsExtension.Tests/` | カタログ出力 (バインディング JSON、ディスパッチ) の生成テストを追加 |
| `AzureFunctionsExtension.Example.Tests/` | ホストを構築して `IFunctionMetadataManager` の結果と Executor のディスパッチを確認するテストを追加 (8.2 の #5 のプロトタイプと同等) |
| `README.md` | Getting started を新SDKの csproj / `global.json` / Program.cs に更新 |

変更不要なもの: 各 `[From*]` のバインド処理、フィルター、`Results`、シリアライザー、`[Function]` スタブの生成ロジック、
Example.Tests のスタブ直接呼び出しテスト。

### 推奨 Program.cs

```csharp
var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.AddAzureFunctionExtension();
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddTransient<SampleFunction>();

builder.Build().Run();
```

`FunctionsApplication.CreateBuilder` では Worker SDK の自動登録が `CreateBuilder` 内で完了するため、
`AddAzureFunctionExtension()` の呼び出し位置による順序の問題が起きない。

---

## 8. 検証内容

### 8.1 環境

- .NET SDK 10.0.112 (Ubuntu パッケージ)
- ジェネレーターが `Microsoft.CodeAnalysis.CSharp` 5.9.0 を参照しているため、SDK 同梱の Roslyn 5.0 では読み込まれない (`CS9057`)。
  検証ではリポジトリのコピーに `Microsoft.Net.Compilers.Toolset` 5.9.0 を追加して実施した (新SDKとは無関係の既存の前提)。
- `Azure.Functions.Sdk` 1.0.1 のパッケージ内容 (props / targets / タスク DLL) と、Worker 2.52.0 の `Core` / `Grpc` / `Worker` アセンブリを逆コンパイルして確認した。

### 8.2 実施内容と結果

| # | 内容 | 結果 |
| --- | --- | --- |
| 1 | 現行構成 (旧SDK) で Example をビルド | 警告 0。`functions.metadata` に 10 関数、`worker.config.json` は `workerIndexing: false` |
| 2 | 手書き `[Function]` (HTTP / Timer / Queue) を新SDKでビルド | `functions.metadata` 無し、`workerIndexing: true`。Worker SDK 生成コードからバインディング JSON とディスパッチ形式を採取 |
| 3 | Example を新SDKに変更してビルド | `AZFW0110` + `AFE0601` |
| 4 | 3 の Worker 生成コードを確認 | メタデータ 0 件、Executor は空 |
| 5 | 推奨案をプロトタイプ (生成コード相当を手書き) し、ホストを構築して `IFunctionMetadataManager` と `IFunctionExecutor` を確認 | Worker 生成の空プロバイダー + Transformer で関数が追加された。Executor はデコレーターになり、Queue 関数のディスパッチ成功。未知の EntryPoint は内側へ委譲 |
| 6 | 5 を `HostBuilder` 方式でも実施 | 同じ結果 (登録順序の問題なし) |
| 7 | メタデータのソース生成を無効化して 5 を実行 | `FileNotFoundException: Function metadata file not found.` |
| 8 | csproj にのみ SDK バージョンを指定して並列ビルド | `azure_functions.g.csproj` で `MSB4236`。`-m:1` は成功、`global.json` 固定で解消 |
| 9 | Example.Tests を新SDK版 Example に対して実行 | 15 件成功 |

### 8.3 未検証事項

- Functions Host (Core Tools) を使った E2E 実行 (Core Tools のダウンロード元に接続できなかったため)。
  ホストへ返るメタデータはワーカー内の `IFunctionMetadataManager` の結果で確認しており、ホスト側の処理は Worker SDK 生成の関数と同じ経路になる。
- Azure へのデプロイ (Flex Consumption 等)、Native AOT 発行。

---

## 9. リスク・留意点

- `Azure.Functions.Sdk` は 1.0 系になったばかりで、プロパティやターゲットの変更が起こり得る。
- 本方式は、デコレーターが `IFunctionExecutor` の最後の登録になることに依存する。順序が逆になると Worker SDK 生成の Executor が使われ、
  未知の EntryPoint は例外なしで何もしないため気付きにくい。5.4 の起動時検証で検出する。
- Worker SDK が将来、生成コードに対する公式の連携手段を提供した場合は、そちらへの移行を検討する。
- in-process モデルのサポートは 2026-11-10 で終了予定で、isolated worker + 新SDK が今後の標準になる。

---

## 10. 参考

- NuGet: [Azure.Functions.Sdk](https://www.nuget.org/packages/Azure.Functions.Sdk) (パッケージ README / release notes を参照)
- NuGet: [Microsoft.Azure.Functions.Worker](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker)
- GitHub: [Azure/azure-functions-dotnet-worker](https://github.com/Azure/azure-functions-dotnet-worker) (SDK ルール: `docs/sdk-rules/`)
- Microsoft Learn: [Guide for running C# Azure Functions in an isolated worker process](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
