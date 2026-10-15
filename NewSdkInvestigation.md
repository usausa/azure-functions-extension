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
5. 実装プランは [10 章](#10-実装プラン) にまとめた。ランタイム側のコード骨子と生成コードの形は、スクラッチ環境でビルド (警告 0) と動作を確認済み。

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

### 5.2 ジェネレーターが追加で出力するコード

アプリケーションのアセンブリに 1 ファイル (`AzureFunctionsExtension.Generated.FunctionCatalog.g.cs`) を出力する。
以下は HTTP 関数と Queue 関数を 1 つずつ持つ `MyApp` の場合の出力イメージ (同じ形のコードを Example でコンパイル・実行して確認済み)。

```csharp
// <auto-generated />
#nullable enable
#pragma warning disable CS0612, CS0618

namespace AzureFunctionsExtension.Generated;

internal sealed class FunctionCatalog : global::AzureFunctionsExtension.Hosting.IGeneratedFunctionCatalog
{
    private const string ScriptFile = "MyApp.dll";

    [global::System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        global::AzureFunctionsExtension.Hosting.GeneratedFunctionCatalog.Register(new FunctionCatalog());
    }

    public void AddMetadata(global::System.Collections.Generic.IList<global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> metadata)
    {
        metadata.Add(new global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.DefaultFunctionMetadata
        {
            Language = "dotnet-isolated",
            Name = "Hello",
            EntryPoint = "MyApp.SampleFunction.Hello_Handler",
            ScriptFile = ScriptFile,
            RawBindings = new global::System.Collections.Generic.List<string>
            {
                "{\"name\":\"req\",\"type\":\"httpTrigger\",\"direction\":\"In\",\"authLevel\":\"Anonymous\",\"methods\":[\"get\"],\"route\":\"hello\"}",
                "{\"name\":\"$return\",\"type\":\"http\",\"direction\":\"Out\"}",
            },
        });
        metadata.Add(new global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.DefaultFunctionMetadata
        {
            Language = "dotnet-isolated",
            Name = "ProcessMessage",
            EntryPoint = "MyApp.QueueFunction.ProcessMessage_Handler",
            ScriptFile = ScriptFile,
            RawBindings = new global::System.Collections.Generic.List<string>
            {
                "{\"name\":\"message\",\"type\":\"queueTrigger\",\"direction\":\"In\",\"queueName\":\"my-queue\",\"dataType\":\"String\"}",
            },
        });
    }

    public global::System.Threading.Tasks.ValueTask<bool> TryExecuteAsync(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        switch (context.FunctionDefinition.EntryPoint)
        {
            case "MyApp.SampleFunction.Hello_Handler":
                return __Execute0__(context);
            case "MyApp.QueueFunction.ProcessMessage_Handler":
                return __Execute1__(context);
            default:
                return new global::System.Threading.Tasks.ValueTask<bool>(false);
        }
    }

    private static async global::System.Threading.Tasks.ValueTask<bool> __Execute0__(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        var values = await __BindAsync__(context);
        global::Microsoft.Azure.Functions.Worker.FunctionContextBindingFeatureExtensions.GetInvocationResult(context).Value =
            await global::MyApp.SampleFunction.Hello_Handler((global::Microsoft.AspNetCore.Http.HttpRequest)values[0]!, context);
        return true;
    }

    private static async global::System.Threading.Tasks.ValueTask<bool> __Execute1__(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        var values = await __BindAsync__(context);
        await global::MyApp.QueueFunction.ProcessMessage_Handler((string)values[0]!, context);
        return true;
    }

    private static async global::System.Threading.Tasks.ValueTask<object?[]> __BindAsync__(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        var feature = context.Features.Get<global::Microsoft.Azure.Functions.Worker.Context.Features.IFunctionInputBindingFeature>() ??
            throw new global::System.InvalidOperationException("IFunctionInputBindingFeature is not available.");
        var result = await feature.BindFunctionInputAsync(context);
        return result.Values;
    }
}
```

- `ScriptFile` は `Compilation.AssemblyName + ".dll"` (Worker SDK の生成コードと同じ規則)。
- `EntryPoint` は `名前空間.クラス名.メソッド名` (既存スタブ `Xxx_Handler`)。名前はエスケープ前のメタデータ名を使う (`@class` → `class`)。
- 呼び出し部分は Worker SDK の `DirectFunctionExecutor` と同じ形 (`BindFunctionInputAsync` → スタブ呼び出し → HTTP のみ `GetInvocationResult().Value` へ設定)。
- `TryExecuteAsync` は非同期メソッドにせず、一致しない場合は同期的に `false` を返す (他の関数の呼び出しで状態機械を作らない)。
- JSON 文字列はジェネレーター内で組み立ててエスケープし、C# リテラル化は既存と同じ `CSharpLiteral.Format` を使う。
- 利用者の `LangVersion` に依存しないよう、コレクション式や raw 文字列リテラルは使わない。

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

以下のコードは、リポジトリの解析設定 (`AnalysisMode=All`、StyleCop、`IsAotCompatible`) で Release ビルドして警告 0 を確認済み。

```csharp
// Hosting/IGeneratedFunctionCatalog.cs
namespace AzureFunctionsExtension.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedFunctionCatalog
{
    void AddMetadata(IList<IFunctionMetadata> metadata);

    ValueTask<bool> TryExecuteAsync(FunctionContext context);
}
```

```csharp
// Hosting/GeneratedFunctionCatalog.cs (生成コードのモジュール初期化子から登録される静的レジストリ)
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedFunctionCatalog
{
    private static IGeneratedFunctionCatalog[] catalogs = [];

    internal static IReadOnlyList<IGeneratedFunctionCatalog> Catalogs => Volatile.Read(ref catalogs);

    public static void Register(IGeneratedFunctionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        while (true)
        {
            var current = Volatile.Read(ref catalogs);
            if (current.Any(x => x.GetType() == catalog.GetType()))
            {
                return;
            }

            if (Interlocked.CompareExchange(ref catalogs, [.. current, catalog], current) == current)
            {
                return;
            }
        }
    }
}
```

```csharp
// Hosting/ExtensionFunctionExecutorState.cs (デコレーターが使われたかを記録する)
internal sealed class ExtensionFunctionExecutorState
{
    public bool Attached { get; set; }
}
```

```csharp
// Hosting/ExtensionFunctionMetadataTransformer.cs
internal sealed class ExtensionFunctionMetadataTransformer : IFunctionMetadataTransformer
{
    private readonly IServiceProvider serviceProvider;

    private readonly ExtensionFunctionExecutorState state;

    private readonly IReadOnlyList<IGeneratedFunctionCatalog>? catalogs;

    public ExtensionFunctionMetadataTransformer(IServiceProvider serviceProvider, ExtensionFunctionExecutorState state, IReadOnlyList<IGeneratedFunctionCatalog>? catalogs = null)
    {
        this.serviceProvider = serviceProvider;
        this.state = state;
        this.catalogs = catalogs;
    }

    public string Name => "AzureFunctionsExtension";

    public void Transform(IList<IFunctionMetadata> original)
    {
        // Executor のチェーンを構築させ、デコレーターが含まれているかを確認する
        _ = serviceProvider.GetRequiredService<IFunctionExecutor>();
        if (!state.Attached)
        {
            throw new InvalidOperationException("The function executor of AzureFunctionsExtension is not used. Call AddAzureFunctionExtension() after ConfigureFunctionsWebApplication().");
        }

        foreach (var catalog in catalogs ?? GeneratedFunctionCatalog.Catalogs)
        {
            catalog.AddMetadata(original);
        }
    }
}
```

```csharp
// Hosting/ExtensionFunctionExecutor.cs
internal sealed class ExtensionFunctionExecutor : IFunctionExecutor
{
    private readonly IFunctionExecutor? inner;

    private readonly IReadOnlyList<IGeneratedFunctionCatalog>? catalogs;

    public ExtensionFunctionExecutor(IFunctionExecutor? inner, ExtensionFunctionExecutorState state, IReadOnlyList<IGeneratedFunctionCatalog>? catalogs = null)
    {
        this.inner = inner;
        this.catalogs = catalogs;
        state.Attached = true;
    }

    public async ValueTask ExecuteAsync(FunctionContext context)
    {
        foreach (var catalog in catalogs ?? GeneratedFunctionCatalog.Catalogs)
        {
            if (await catalog.TryExecuteAsync(context).ConfigureAwait(false))
            {
                return;
            }
        }

        if (inner is null)
        {
            throw new InvalidOperationException($"Function is not found. entryPoint=[{context.FunctionDefinition.EntryPoint}]");
        }

        await inner.ExecuteAsync(context).ConfigureAwait(false);
    }
}
```

`AddAzureFunctionExtension()` での登録 (`ServiceCollectionExtensions.cs` に追加):

```csharp
private static void AddFunctionCatalog(IServiceCollection services)
{
    // 複数回呼ばれても二重登録しない (Transformer が 2 つになると関数名が重複してホストが起動しない)
    if (services.Any(static x => x.ServiceType == typeof(ExtensionFunctionExecutorState)))
    {
        return;
    }

    // 直前の IFunctionExecutor 登録 (Worker SDK 生成の DirectFunctionExecutor) を内側に包む
    var previous = services.LastOrDefault(static x => (x.ServiceType == typeof(IFunctionExecutor)) && !x.IsKeyedService);
    services.AddSingleton(static _ => new ExtensionFunctionExecutorState());
    services.AddSingleton<IFunctionMetadataTransformer>(static sp => new ExtensionFunctionMetadataTransformer(sp, sp.GetRequiredService<ExtensionFunctionExecutorState>()));
    services.AddSingleton<IFunctionExecutor>(sp => new ExtensionFunctionExecutor(
        previous is null ? null : ResolveExecutor(sp, previous),
        sp.GetRequiredService<ExtensionFunctionExecutorState>()));
}

private static IFunctionExecutor ResolveExecutor(IServiceProvider serviceProvider, ServiceDescriptor descriptor) =>
    (IFunctionExecutor)(descriptor.ImplementationInstance ??
        descriptor.ImplementationFactory?.Invoke(serviceProvider) ??
        ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!));
```

- `ServiceDescriptor.ImplementationType` は `DynamicallyAccessedMembers(PublicConstructors)` 注釈付きのため、AOT/トリミング解析の警告は出ない (実測)。
- キー付き登録の `ImplementationType` は参照すると例外になるため、`IsKeyedService` を除外する。
- 内部型はファクトリーラムダで生成する (`AddSingleton<TService, TImplementation>()` だと `CA1812` の対象になり得るため)。
- カタログは使用時にレジストリから読む。`AddAzureFunctionExtension()` の時点では、テストなどでモジュール初期化子がまだ実行されていない場合があるため。
- 登録順序が逆 (`AddAzureFunctionExtension()` が Worker の自動登録より前) になると、デコレーターが上書きされて関数が無言で何もしなくなる。
  Transformer でデコレーターが Executor チェーンに含まれているか (`ExtensionFunctionExecutorState.Attached`) を確認し、含まれていなければ起動時に例外にする。
  型の比較ではなく状態で確認するため、他のライブラリがさらに外側をデコレートしていても誤検出しない。
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

概要のみ。ファイルごとの詳細な変更内容と作業順序は [10 章](#10-実装プラン) を参照。

| ファイル | 変更内容 |
| --- | --- |
| `AzureFunctionsExtension.props` | 5.6 の通り |
| `AzureFunctionsExtension.Generator/FunctionGenerator.cs` | 旧 `AFE0601` 判定を削除し 5.7 の判定を追加。全モデルを集約してカタログを 1 ファイル出力 |
| `AzureFunctionsExtension.Generator/FunctionSourceBuilder.cs` (または新規ビルダー) | カタログ (メタデータ JSON + ディスパッチ) の出力 |
| `AzureFunctionsExtension.Generator/Diagnostics.cs` / `Diagnostics.md` | 5.7 の通り |
| `AzureFunctionsExtension/` | `Hosting` 名前空間 (レジストリ、Transformer、Executor デコレーター)、`ServiceCollectionExtensions` の登録追加、`Assembly.cs` に `InternalsVisibleTo` 追加 |
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
| 10 | 5.4 のランタイムコードをライブラリに追加して Release ビルド | 警告 0 (AOT/トリミング解析を含む) |
| 11 | 5.2 の形の生成コード (手書き) を Example に置き、`FunctionsApplication` 方式と `HostBuilder` 方式で実行 | モジュール初期化子経由でカタログが登録され、メタデータ 3 件を追加。Timer / Queue のディスパッチ成功。未知の EntryPoint は内側の Executor へ委譲 |
| 12 | `ConfigureServices` を `ConfigureFunctionsWebApplication` より前に書いた `HostBuilder` で実行 (登録順序が逆) | 最終的な Executor が Worker SDK 生成の `DirectFunctionExecutor` になり、Transformer で `InvalidOperationException` (検出できることを確認) |

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

## 10. 実装プラン

### 10.1 前提と方針

- 対象は新SDK (`Azure.Functions.Sdk` 1.0.1 以降) のみ。旧 `Microsoft.Azure.Functions.Worker.Sdk` 向けの設定と診断は削除する。
- 必要な Worker は `Microsoft.Azure.Functions.Worker` 2.1.0 以降 (`IFunctionMetadataTransformer`)。ライブラリの参照は現行の 2.52.0 のまま。
- 利用者向けの公開 API (`[AzureFunction]`、各属性、`Results`、フィルター、`AddAzureFunctionExtension()`) は変えない。
  利用者側の変更は csproj、`global.json`、(推奨) Program.cs のみ。
- 既存の `[Function]` スタブ (`Xxx_Handler`) は、エントリーポイントとして生成内容を変えずに残す。
- 各フェーズの完了時点で、ソリューションのビルドが警告 0 かつ全テスト成功の状態を保つ。
- ビルドには Roslyn 5.9 以上を含む .NET SDK が必要 (ジェネレーターの既存の前提)。

### 10.2 フェーズ一覧

| フェーズ | 内容 | 主な成果物 | 完了条件 |
| --- | --- | --- | --- |
| 1 | ランタイムライブラリ | `AzureFunctionsExtension/Hosting/*`、`ServiceCollectionExtensions.cs` | 単体テスト成功。既存の Example (旧SDK) も動作 |
| 2 | ジェネレーターの下準備 (挙動変更なし) | `FunctionModel.MetadataName`、`HandlerModelExtensions` | 既存テストが無変更で成功 |
| 3 | カタログ生成 | `JsonText`、`BindingJsonBuilder`、`FunctionCatalogSourceBuilder`、`FunctionGenerator` | 生成テスト成功 |
| 4 | 新SDKへの切り替え | 診断 (`AFE0601`/`AFE0602`)、`AzureFunctionsExtension.props`、Example の csproj / `global.json` / Program.cs | 新SDKで警告 0、全テスト成功 |
| 5 | Example.Tests の結合テスト | `FunctionCatalogTests.cs`、`FakeFunctionContext` 拡張 | テスト成功 |
| 6 | ドキュメント | `README.md`、`Diagnostics.md` | — |
| 7 | 総合検証 | — | 10.9 のチェックリストを全て満たす |
| 8 (任意) | `AFE0205` の拡張 | `FunctionGenerator` | テスト成功 |

フェーズ 4 は分割しない。旧 `AFE0601` は新SDKで必ずエラーになり、新 `AFE0601` は旧SDKの Example で必ずエラーになるため、
診断・props・Example の切り替えを 1 コミットで行う必要がある。コミットはフェーズ単位とする。

### 10.3 フェーズ 1: ランタイムライブラリ

| ファイル | 種別 | 内容 |
| --- | --- | --- |
| `AzureFunctionsExtension/Hosting/IGeneratedFunctionCatalog.cs` | 新規 | 5.4 の通り (public、`EditorBrowsable(Never)`) |
| `AzureFunctionsExtension/Hosting/GeneratedFunctionCatalog.cs` | 新規 | 5.4 の通り。型が同じカタログの重複登録は無視する |
| `AzureFunctionsExtension/Hosting/ExtensionFunctionExecutorState.cs` | 新規 | 5.4 の通り (internal) |
| `AzureFunctionsExtension/Hosting/ExtensionFunctionMetadataTransformer.cs` | 新規 | 5.4 の通り (internal)。テスト用にカタログを直接渡せる引数を持つ |
| `AzureFunctionsExtension/Hosting/ExtensionFunctionExecutor.cs` | 新規 | 5.4 の通り (internal)。同上 |
| `AzureFunctionsExtension/ServiceCollectionExtensions.cs` | 変更 | 両方の `AddAzureFunctionExtension` オーバーロードから `AddFunctionCatalog(services)` を呼ぶ (5.4) |
| `AzureFunctionsExtension/Assembly.cs` | 変更 | `[assembly: InternalsVisibleTo("AzureFunctionsExtension.Tests")]` を追加 |

このフェーズ単体では、旧SDKの Example にも影響しない
(Executor のソース生成が無効なので、内側は Worker 既定の `DefaultFunctionExecutor` になり、カタログが無いためそのまま委譲される)。

テスト (`AzureFunctionsExtension.Tests/HostingTests.cs` 新規。カタログと `FunctionContext` は最小限のフェイクを用意する):

| # | テスト | 確認内容 |
| --- | --- | --- |
| H1 | Transformer がカタログのメタデータを追加する | 複数カタログの登録順に `AddMetadata` が呼ばれ、元のリストの要素が残る |
| H2 | デコレーターが使われていない場合に Transformer が例外を出す | `AddAzureFunctionExtension()` の後に別の `IFunctionExecutor` を登録すると `InvalidOperationException` |
| H3 | Executor がカタログで処理できた場合に内側を呼ばない | `TryExecuteAsync` が `true` を返したら終了 |
| H4 | Executor がどのカタログでも処理できない場合に内側へ委譲する | 内側のフェイクが呼ばれる |
| H5 | 内側が無く処理もできない場合に例外を出す | `InvalidOperationException` (メッセージに EntryPoint を含む) |
| H6 | 既存の Executor 登録を包む | 型登録 / ファクトリー登録 / インスタンス登録の 3 通り |
| H7 | `AddAzureFunctionExtension()` の複数回呼び出し | Transformer が 1 件、Executor が二重に包まれない |
| H8 | キー付きの `IFunctionExecutor` 登録 | 包む対象にならず、例外も出ない |
| H9 | `GeneratedFunctionCatalog.Register` の重複 | 同じ型は 1 回だけ登録される (静的状態のため、このテスト専用のフェイク型を使う) |

### 10.4 フェーズ 2: ジェネレーターの下準備 (挙動変更なし)

| ファイル | 内容 |
| --- | --- |
| `Models/FunctionModel.cs` | `string MetadataName` (例: `MyApp.Functions.SampleFunction`。エスケープ無し) を追加 |
| `FunctionModelBuilder.cs` | `MetadataName` を算出して設定する (下記) |
| `Models/HandlerModelExtensions.cs` (新規) | 既定値の解決を共通化する拡張メソッド (下記) |
| `FunctionSourceBuilder.cs` | `?? "get"` などの既定値処理と `_Handler` の付与を拡張メソッド経由に置き換える (出力は同一) |

```csharp
// FunctionModelBuilder: ToDisplayString() は @ 付きの名前を返す場合があるため、メタデータ名を連結する
private static string GetMetadataName(INamedTypeSymbol symbol)
{
    var parts = new Stack<string>();
    parts.Push(symbol.MetadataName);
    for (var ns = symbol.ContainingNamespace; (ns is not null) && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
    {
        parts.Push(ns.MetadataName);
    }

    return String.Join(".", parts);
}
```

```csharp
// Models/HandlerModelExtensions.cs: スタブとメタデータで同じ値を使うための唯一の定義
internal static class HandlerModelExtensions
{
    public static string GetHttpMethod(this HandlerModel handler) => handler.HttpMethod ?? "get";

    public static string GetRoute(this HandlerModel handler) => handler.Route ?? handler.MethodName;

    public static string GetAuthorizationLevel(this HandlerModel handler) => handler.AuthorizationLevel ?? "Function";

    public static string GetTimerSchedule(this HandlerModel handler) => handler.TimerSchedule ?? "0 * * * * *";

    public static string GetQueueName(this HandlerModel handler) => handler.QueueName ?? "myqueue";

    public static string GetEntryPointMethodName(this HandlerModel handler) => handler.MethodName + "_Handler";
}
```

完了条件: 既存の生成テスト・診断テスト・キャッシュテストが無変更で全て成功すること (生成コードの差分が無いこと)。

### 10.5 フェーズ 3: カタログ生成

#### ファイル

| ファイル | 種別 | 内容 |
| --- | --- | --- |
| `AzureFunctionsExtension.Generator/JsonText.cs` | 新規 | JSON 文字列のエスケープ (`"`、`\`、`\b` `\f` `\n` `\r` `\t`、その他の制御文字は `\u00XX`。それ以外はそのまま) |
| `AzureFunctionsExtension.Generator/BindingJsonBuilder.cs` | 新規 | `IReadOnlyList<string> Build(HandlerModel handler)`。5.3 の JSON を生成する (プロパティ順も 5.3 の通り) |
| `AzureFunctionsExtension.Generator/FunctionCatalogSourceBuilder.cs` | 新規 | `Build(SourceBuilder builder, string assemblyName, EquatableArray<FunctionModel> models)`。5.2 のコードを出力する |
| `AzureFunctionsExtension.Generator/FunctionGenerator.cs` | 変更 | カタログのパイプラインを追加 (下記) |

#### パイプライン

```csharp
// 既存の collected (ImmutableArray<Result<FunctionModel>>) を再利用する
var assemblyNameProvider = context.CompilationProvider
    .Select(static (compilation, _) => compilation.AssemblyName ?? string.Empty);
var catalogProvider = collected
    .Select(static (results, _) => new EquatableArray<FunctionModel>(SelectModels(results).ToArray()))
    .Combine(assemblyNameProvider);
context.RegisterImplementationSourceOutput(catalogProvider, static (ctx, input) => ExecuteCatalog(ctx, input.Left, input.Right));
```

- `SelectModels` を使い、スタブを生成するクラス (エラーやヒント名衝突のあるクラスを除いたもの) とカタログの対象を一致させる。
- `CompilationProvider` は無関係な編集でも再評価されるが、`Select` の結果 (アセンブリ名) が同じなら以降はキャッシュされる。
- ハンドラーが 1 件も無い場合はカタログを出力しない。
- ヒント名は `AzureFunctionsExtension.Generated.FunctionCatalog` 固定 (既存のクラス単位のヒント名は `_` 区切りのため衝突しない)。

#### 出力規則

| 項目 | 規則 |
| --- | --- |
| 型 | `internal sealed class AzureFunctionsExtension.Generated.FunctionCatalog : IGeneratedFunctionCatalog` |
| 登録 | `[ModuleInitializer]` の `Initialize()` で `GeneratedFunctionCatalog.Register(new FunctionCatalog())` |
| `Name` | `handler.MethodName` (スタブの `[Function]` と同じ) |
| `EntryPoint` | `model.MetadataName + "." + handler.GetEntryPointMethodName()` |
| `ScriptFile` | `assemblyName + ".dll"` を定数として 1 回だけ出力 |
| `RawBindings` | `BindingJsonBuilder` の結果を `CSharpLiteral.Format` でリテラル化 |
| 並び順 | モデルの順 → ハンドラーの順。`__ExecuteN__` の N はカタログ全体の通し番号 |
| ディスパッチ (HTTP) | `GetInvocationResult(context).Value = await Stub((HttpRequest)values[0]!, context)` |
| ディスパッチ (Timer) | `await Stub((TimerInfo)values[0]!, context)` |
| ディスパッチ (Queue) | `await Stub((string)values[0]!, context)` |
| スタブ呼び出し | `{model.FunctionType.FullName}.{CSharpIdentifier.Escape(handler.GetEntryPointMethodName())}` |
| その他 | `// <auto-generated />`、`#nullable enable`、`#pragma warning disable CS0612, CS0618` (既存の生成ファイルと同じ) |

#### テスト (`AzureFunctionsExtension.Tests/CatalogTests.cs` 新規)

| # | テスト | 確認内容 |
| --- | --- | --- |
| C1 | HTTP のメタデータ | 認可レベル既定 (`Function`) と指定時 (`Anonymous`) の JSON、`$return` の http 出力 |
| C2 | Timer のメタデータ | `schedule` |
| C3 | Queue のメタデータ | `connection` の有無で出力が変わる、`dataType` は `String` |
| C4 | JSON のエスケープ | `"`、`\`、制御文字を含むルート。`BindingJsonBuilder` を直接呼び、`JsonDocument.Parse` で往復確認 |
| C5 | `ScriptFile` | `WithAssemblyName("MyApp")` で `"MyApp.dll"` (`CompilationHelper` にアセンブリ名指定のオーバーロードを追加) |
| C6 | `EntryPoint` | キーワード名 (`namespace @class;`、`@event` クラス、`@default` メソッド) とグローバル名前空間のクラス |
| C7 | ディスパッチ | ハンドラーごとの `case` とスタブ呼び出し。`GetInvocationResult` は HTTP のみ |
| C8 | カタログを出力しない条件 | `[AzureFunction]` が無い、または全クラスがエラー (`AFE0001` など) |
| C9 | ヒント名衝突の除外 | `AFE0501` のクラスはカタログにも含まれない |
| C10 | 生成コードのコンパイル | HTTP のみのソースで `GetProblemIds` が空 (生成ファイルの警告も問題として検出される) |

`PipelineCacheTests` の既存テスト (無関係な編集で全出力がキャッシュされる) がカタログ出力も対象にするため、追加は不要。失敗した場合はパイプラインを見直す。

### 10.6 フェーズ 4: 新SDKへの切り替え

#### 診断 (`Diagnostics.cs`)

```csharp
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
```

#### ジェネレーター (`FunctionGenerator.cs`)

```csharp
// Models/BuildConfigModel.cs
internal sealed record BuildConfigModel(bool UsingAzureFunctionsSdk, bool MetadataSourceGenEnabled);

// Initialize: 旧 workerSourceGenProvider を置き換える
var buildConfigProvider = context.AnalyzerConfigOptionsProvider
    .Select(static (provider, _) => new BuildConfigModel(
        IsTrue(provider.GlobalOptions, "UsingAzureFunctionsSdk"),
        IsTrue(provider.GlobalOptions, "FunctionsEnableMetadataSourceGen") &&
        IsTrue(provider.GlobalOptions, "FunctionsAutoRegisterGeneratedMetadataProvider")));
context.RegisterSourceOutput(buildConfigProvider.Combine(treeProvider), static (ctx, input) => ReportBuildConfiguration(ctx, input.Left, input.Right));

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
```

- 削除: `IsWorkerSourceGenEnabled`、`ReportWorkerSourceGen`、`Diagnostics.WorkerSourceGenEnabled`。
- `[AzureFunction]` をクラスライブラリに置いた場合も `AFE0601` になる (クラスライブラリのモジュール初期化子は、ホストがメタデータを要求する時点で実行されている保証が無いため、意図的に対象外とする)。

#### MSBuild (`AzureFunctionsExtension.props`)

```xml
<Project>

  <!-- Lets AzureFunctionsExtension.Generator detect Azure.Functions.Sdk (AFE0601) -->
  <ItemGroup>
    <CompilerVisibleProperty Include="UsingAzureFunctionsSdk" />
  </ItemGroup>

</Project>
```

`AzureFunctionsExtension.targets` は空のまま変更しない。`FunctionsEnableMetadataSourceGen` などは SDK が `CompilerVisibleProperty` に登録済み。

#### Example

`AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj`:

```xml
<Project Sdk="Azure.Functions.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <CodeAnalysisRuleSet>..\Analyzers.ruleset</CodeAnalysisRuleSet>
  </PropertyGroup>

  <Import Project="..\AzureFunctionsExtension.props" />
  <Import Project="..\AzureFunctionsExtension.targets" />

  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.1.1" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.3.1" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues" Version="5.5.5" />
    <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" Version="3.1.2" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" Version="2.51.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AzureFunctionsExtension\AzureFunctionsExtension.csproj" />
    <ProjectReference Include="..\AzureFunctionsExtension.Generator\AzureFunctionsExtension.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>

</Project>
```

- 削除: `AzureFunctionsVersion`、`OutputType`、`Microsoft.Azure.Functions.Worker.Sdk`、`host.json` / `local.settings.json` のコピー指定 (SDK が自動で処理する)。

`global.json` (リポジトリ直下に新規作成):

```json
{
  "msbuild-sdks": {
    "Azure.Functions.Sdk": "1.0.1"
  }
}
```

`AzureFunctionsExtension.Example/Program.cs`:

```csharp
using AzureFunctionsExtension;
using AzureFunctionsExtension.Example.Functions;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

KeepExampleTypesAlive();

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.AddAzureFunctionExtension();
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddTransient<HttpFunction>();
builder.Services.AddTransient<QueueFunction>();
builder.Services.AddTransient<FilterFunction>();
builder.Services.AddTransient<LoggingFilter>();

builder.Build().Run();

static void KeepExampleTypesAlive()
{
    // 現行と同じ
}
```

- トップレベルに `await` を置かないこと (置くと `Run()` に `CA1849` が出る。プロトタイプで確認)。

#### テストの更新

- `AzureFunctionsExtension.Tests/CompilationHelper.cs`: `Runner` に既定のグローバルオプションを追加し、既存テストが新SDK前提の構成で動くようにする。

  ```csharp
  private static GeneratorTestRunner Runner => GeneratorTestRunner
      .For<FunctionGenerator>()
      // (既存の WithReference)
      .WithGlobalOption("build_property.UsingAzureFunctionsSdk", "true")
      .WithGlobalOption("build_property.FunctionsEnableMetadataSourceGen", "true")
      .WithGlobalOption("build_property.FunctionsAutoRegisterGeneratedMetadataProvider", "true");
  ```

  `WithGlobalOption` は同じキーを上書きするため、既存の `GetProblemIds(source, properties)` で個別に上書きできる。

- `AzureFunctionsExtension.Tests/DiagnosticTests.cs`: 旧 `AFE0601` の 3 テストを削除し、次を追加する。

| # | テスト | 期待値 |
| --- | --- | --- |
| D1 | `UsingAzureFunctionsSdk=false` | `["AFE0601"]` |
| D2 | 既定 (新SDK構成) | 空 |
| D3 | `FunctionsEnableMetadataSourceGen=false` | `["AFE0602"]` |
| D4 | `FunctionsAutoRegisterGeneratedMetadataProvider=false` | `["AFE0602"]` |
| D5 | `UsingAzureFunctionsSdk=false` かつメタデータ生成無効 | `["AFE0601"]` のみ |
| D6 | `[AzureFunction]` が無いソースで `UsingAzureFunctionsSdk=false` | 空 |

完了条件: `dotnet build AzureFunctionsExtension.slnx` (既定の並列ビルド) が Debug / Release ともに警告 0、全テスト成功。
Example の出力に `worker.config.json` (`workerIndexing: true`)、`extensions.json`、`host.json` があり、`functions.metadata` が無いこと。

### 10.7 フェーズ 5: Example.Tests の結合テスト

| ファイル | 種別 | 内容 |
| --- | --- | --- |
| `AzureFunctionsExtension.Example.Tests/FakeFunctionContext.cs` | 変更 | EntryPoint と入力値を受け取るコンストラクターを追加し、`FunctionDefinition` (EntryPoint のみ) と `Features` (`IFunctionInputBindingFeature` を返す) を実装する。既存のコンストラクターの挙動は変えない |
| `AzureFunctionsExtension.Example.Tests/FunctionCatalogTests.cs` | 新規 | 下表 |

テストクラスのコンストラクターで `RuntimeHelpers.RunModuleConstructor(typeof(HttpFunction).Module.ModuleHandle)` を呼び、
Example のモジュール初期化子 (カタログ登録) を確実に実行させる。サービスは既存の `HandlerTestHost.CreateServices()` を使う。

| # | テスト | 確認内容 |
| --- | --- | --- |
| E1 | メタデータの一覧 | Transformer の結果が Example の 10 関数 (名前、EntryPoint、`ScriptFile` = `AzureFunctionsExtension.Example.dll`) と一致する |
| E2 | バインディング JSON | 全関数の `RawBindings` が JSON として解析でき、トリガー種別が期待通り |
| E3 | EntryPoint の実在 | Worker の `MethodInfoLocator` と同じく、型名とメソッド名に分割してリフレクションで public static メソッドが見つかる |
| E4 | Queue のディスパッチ | `ProcessMessage_Handler` が呼ばれ、例外が出ない |
| E5 | Timer のディスパッチ | `new TimerInfo()` を入力にして `Timer_Handler` が呼ばれる |
| E6 | 未知の EntryPoint | 内側の Executor が無いため `InvalidOperationException` |

HTTP のディスパッチは `GetInvocationResult()` が internal な `IFunctionBindingsFeature` を必要とするため単体テストしない
(生成コードは C7 で、実行は 10.9 の E2E で確認する)。

### 10.8 フェーズ 6: ドキュメント

| ファイル | 内容 |
| --- | --- |
| `README.md` | Getting started を新SDKの csproj、`global.json`、`FunctionsApplication.CreateBuilder` 形式の Program.cs に更新。要件 (`Azure.Functions.Sdk` 1.0.1 以降、`global.json` での固定) を追記 |
| `Diagnostics.md` | Build configuration の表を `AFE0601` (Azure.Functions.Sdk が必要)、`AFE0602` (メタデータのソース生成が無効) に更新 |
| `NewSdkInvestigation.md` | 実装完了後、残課題 (E2E 未確認など) を更新 |

`Diagnostics.md` の更新後の行:

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0601 | ❌ Error | The project does not use `Azure.Functions.Sdk` | Set the project SDK to `Azure.Functions.Sdk` |
| AFE0602 | ❌ Error | The Worker SDK metadata source generation (`FunctionsEnableMetadataSourceGen` or `FunctionsAutoRegisterGeneratedMetadataProvider`) is disabled, so the generated functions are not indexed | Remove the settings that disable them |

### 10.9 フェーズ 7: 総合検証

- [ ] `dotnet build AzureFunctionsExtension.slnx -c Debug` と `-c Release` が警告 0 (既定の並列ビルド)
- [ ] テストが全て成功 (`dotnet run --project AzureFunctionsExtension.Tests`、`dotnet run --project AzureFunctionsExtension.Example.Tests`)
- [ ] `dotnet pack AzureFunctionsExtension -c Release` のパッケージに、新しい `build/AzureFunctionsExtension.props` と `analyzers/dotnet/cs/AzureFunctionsExtension.Generator.dll` が含まれる
- [ ] パッケージ利用の確認: ローカルフィードの nupkg を参照する新SDKのプロジェクトを別途作り、
      `AZFW0110` / `AFE0601` / `AFE0602` が出ないこと、`FunctionCatalog` が生成されることを確認 (NuGet の props の読み込み順の確認を兼ねる)
- [ ] 異常系: `Microsoft.NET.Sdk` + `Microsoft.Azure.Functions.Worker.Sdk` の構成で `AFE0601`、`FunctionsEnableMetadataSourceGen=false` で `AFE0602`
- [ ] E2E: Core Tools と Azurite を用意し、Example の出力ディレクトリで `func start`
  - ホストのログに 10 関数が表示される
  - `GET /api/ping` などの HTTP 関数が期待通りの応答を返す
  - `my-queue` へのメッセージが処理される
  - Timer 関数が実行される (管理 API `POST /admin/functions/Timer` でも可)
- [ ] 登録順序の誤り: `ConfigureServices` を `ConfigureFunctionsWebApplication` より前に書くと、起動時に `InvalidOperationException` のメッセージが出る

### 10.10 フェーズ 8 (任意): `AFE0205` の拡張

- 同じコンパイル内の手書き `[Function]` の関数名を収集するプロバイダーを追加する
  (`ForAttributeWithMetadataName("Microsoft.Azure.Functions.Worker.FunctionAttribute", ...)`。
  本ライブラリが生成するスタブはジェネレーターの入力に含まれないため、自身と重複検出されることはない)。
- `[AzureFunction]` のハンドラー名と大文字小文字を区別せずに一致した場合に `AFE0205` を報告する。
- 重複したままだと、ホストがメタデータ読み込み時に関数名の重複エラーで起動しない。

### 10.11 実装前に決めること

| # | 事項 | 推奨 |
| --- | --- | --- |
| 1 | `AFE0601` の意味を変えて再利用するか、新しい ID にするか | 再利用 (ベータ期間中のため。`Diagnostics.md` で明記する) |
| 2 | スタブの `[Function]` 属性を残すか | 残す (メタデータには使われないが害は無く、差分を最小にできる) |
| 3 | `global.json` に `"test": { "runner": "Microsoft.Testing.Platform" }` も追加するか (.NET 10 SDK の `dotnet test` で Microsoft.Testing.Platform を使うための設定) | 本対応とは分ける |
| 4 | パッケージバージョン (`Directory.Build.props`) | メンテナーの判断 (例: 2.0.0-beta10) |
| 5 | フェーズ 8 を今回含めるか | 後続の対応とする |

### 10.12 規模の目安

| フェーズ | 追加・変更の目安 |
| --- | --- |
| 1 | ライブラリ約 150 行、テスト約 250 行 |
| 2 | 約 60 行 (置き換え中心) |
| 3 | ジェネレーター約 300 行、テスト約 350 行 |
| 4 | 約 150 行 (診断・props・Example・テスト更新) |
| 5 | テスト約 200 行 |
| 6 | ドキュメントのみ |

---

## 11. 参考

- NuGet: [Azure.Functions.Sdk](https://www.nuget.org/packages/Azure.Functions.Sdk) (パッケージ README / release notes を参照)
- NuGet: [Microsoft.Azure.Functions.Worker](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker)
- GitHub: [Azure/azure-functions-dotnet-worker](https://github.com/Azure/azure-functions-dotnet-worker) (SDK ルール: `docs/sdk-rules/`)
- Microsoft Learn: [Guide for running C# Azure Functions in an isolated worker process](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
