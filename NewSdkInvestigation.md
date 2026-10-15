# Azure Functions 新SDK (`Azure.Functions.Sdk`) 対応調査

- 調査・検証日: 2026-10-15
- 対象バージョン
  - `Azure.Functions.Sdk` 1.0.1 (2026-09-04 公開。1.0.0 GA は 2026-09-01)
  - `Microsoft.Azure.Functions.Worker` 2.52.0
  - `Microsoft.Azure.Functions.Worker.Sdk.Generators` 1.4.0 / `Microsoft.Azure.Functions.Worker.Sdk.Analyzers` 1.2.2 (新SDKが暗黙参照)
  - Functions Host 4.1054.250.6 (E2E 検証に使用したコンテナーイメージ `mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0`)
- 前提
  - 現行 SDK (`Microsoft.Azure.Functions.Worker.Sdk`) との互換性は考慮せず、新SDKのみを対象とする
  - 方式は推奨案 (5 章) で確定し、実装前に決める事項も推奨どおりとする (9.1)

---

## 1. 結論

1. 新SDKでは **ワーカーインデックスが常に有効** で、ビルド時の `functions.metadata` は **生成されない**。
   関数メタデータはワーカープロセス内の `IFunctionMetadataProvider` (Worker SDK のソースジェネレーターが生成) からホストへ返される。
2. 本ライブラリは「別のソースジェネレーターが生成した `[Function]` メソッドは Worker SDK のジェネレーターから見えない」問題を、
   **ワーカーインデックス無効化 + ビルド時 `functions.metadata` (Mono.Cecil でコンパイル済みアセンブリを走査)** で回避している。
   新SDKではこの前提が成り立たないため、**そのままでは動作しない** (実測: `AZFW0110` 警告 + `AFE0601` エラー。
   仮に `AFE0601` を外しても、関数 0 件のメタデータと何も実行しない Executor が登録される)。
3. 対応方式は、Worker の公開拡張ポイント **`IFunctionMetadataTransformer`** (Worker.Core 2.1.0 以降) で生成した関数のメタデータを追加し、
   **`IFunctionExecutor` のデコレーター** で生成した関数を直接呼び出すコードを、本ライブラリのジェネレーターから出力する方式とする。
4. 推奨方式をスクラッチ環境で全フェーズ実装し、**実際の Functions Host (コンテナー) + Azurite の E2E で、現行 (旧SDK) と同一の動作** を確認した
   (関数一覧 10 件、HTTP 18 ケースの応答、Queue / Timer の実行。7.4)。ビルド警告 0、テスト 135 件成功。
5. 詳細検証で、当初の計画から次の点を修正・追加した。
   - ワーカー内の ASP.NET Core ルーティングも `IFunctionMetadataManager` (Transformer 適用後) から HTTP 関数を組み立てる。
     Transformer はホストへの通知だけでなく **ルーティングにも必須**。また重複したメタデータはルートの重複になるため、Transformer に重複除外を入れた (5.4)。
   - Transformer 内での登録順序の検証は、例外がホストのログに出ず「関数が 1 件も無い」状態になるだけで原因が分からないため廃止した。
     代わりに、生成コードの `IAutoConfigureStartup` から起動時検証 (`IHostedService`) を登録し、**登録漏れ・登録順序の誤りを原因付きで起動失敗にする** (5.5)。
   - 手書きの `[Function]` と関数名が重複すると、生成した関数が **警告無しに消える** (実測)。`AFE0205` の拡張を今回の対応に含める (5.7)。
   - ソリューション単位の復元では新SDKの復元後処理が動かず、クリーンビルド時に `AZFW0108` 警告が出る (3.6)。
6. 利用者側は `Sdk="Azure.Functions.Sdk"` への切り替えと、`global.json` での SDK バージョン固定が必要
   (固定しないと並列ビルドで SDK 解決エラーになる)。`AddAzureFunctionExtension()` の呼び出しは必須になる。
7. 検証中に、新SDKとは無関係の既存不具合を見つけた: **`[FromBody]` を使う HTTP 関数は実ホストで常に 500** になる
   (リクエスト本文を同期で読んでいるため。8 章)。別途の対応が必要。

### 必要な変更の要約

| 区分 | 変更内容 |
| --- | --- |
| MSBuild (`AzureFunctionsExtension.props`) | `FunctionsEnableWorkerIndexing` / `FunctionsEnableExecutorSourceGen` の設定を削除。`UsingAzureFunctionsSdk` をジェネレーターから参照可能にする |
| ジェネレーター | 関数カタログ (メタデータ追加 + 実行ディスパッチ + 起動時検証の登録) の生成を追加。`AFE0601` を置き換え、`AFE0602` を追加、`AFE0205` を拡張 |
| ランタイムライブラリ | Transformer、Executor デコレーター、起動時検証、カタログのレジストリを追加し、`AddAzureFunctionExtension()` で登録 |
| Example / テスト / README | 新SDK形式の csproj、`global.json`、`FunctionsApplication.CreateBuilder` 形式の Program.cs に更新。テストを追加・更新 |

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
| AZFW0108 | Warning | 拡張プロジェクトが復元前にビルドされた (3.6) |
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
- `CompilerVisibleProperty` の項目はどこで追加しても有効なため、パッケージの `.props` から `UsingAzureFunctionsSdk` をジェネレーターへ渡せる
  (パッケージ参照の構成で確認。7.5)。

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
ミドルウェア (FunctionsHttpProxyingMiddleware など) → FunctionExecutionMiddleware → IFunctionExecutor.ExecuteAsync(context)
                                                                                   └─ DirectFunctionExecutor  ← Worker SDK のジェネレーターが生成・自動登録

ワーカー内の ASP.NET Core (HTTP 関数のみ)
  Host から転送された HTTP リクエスト
    └─ FunctionsEndpointDataSource  ← IFunctionMetadataManager の結果 (Transformer 適用後) から RawBindings の route / methods でエンドポイントを構築
         └─ FunctionsHttpProxyingMiddleware と HttpContext を受け渡し (DefaultHttpCoordinator)
```

ポイント:

- メタデータ (関数名・EntryPoint・バインディング JSON) はワーカーが返す。ホストによるアセンブリ走査は行われない。
- `IFunctionMetadataProvider` を Worker の既定実装 (`DefaultFunctionMetadataProvider`) にすると `functions.metadata` を読みに行き、
  新SDKではファイルが存在しないため `FileNotFoundException` になる (実測)。
- `IFunctionMetadataTransformer` は Worker.Core **2.1.0 で追加** された公開インターフェイス (2.0.0 には無い)。DI に複数登録でき、
  プロバイダーの結果に対して追加・変更・削除ができる。
- **ワーカー内の ASP.NET Core 統合 (`FunctionsEndpointDataSource`) も `IFunctionMetadataManager` から HTTP ルートを構築する**
  (`Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` 2.1.1 を逆コンパイルして確認)。
  - Transformer で追加しない限り、ホストが HTTP リクエストを転送してもワーカー側にルートが無い。
  - 同じ関数のメタデータが 2 件あるとルートが重複し、リクエストが関数に届かず `Timed out waiting for the function start call` になる (実測。7.3)。
- エントリーポイントのメソッドは実在する `public` メソッドである必要がある (パラメーター情報と `InputConverterAttribute` をリフレクションで取得するため)。
  型は internal でもよい (`Assembly.GetType` で解決される。Example の関数クラスは internal で動作確認済み)。
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
- Executor は未知の EntryPoint に対して **例外を出さず何もしない**。メタデータだけ追加しても関数本体は実行されない。

### 3.5 自動登録の順序

Worker SDK が生成する `IAutoConfigureStartup` 実装は、`ConfigureFunctionsWorkerDefaults()` の中で
`Assembly.GetEntryAssembly().GetTypes()` から探索され (internal 型も対象。`Activator.CreateInstance` で生成)、`ConfigureServices` コールバックとして登録される。

| ホスト構築方法 | 自動登録のタイミング | 利用者の `AddAzureFunctionExtension()` との順序 |
| --- | --- | --- |
| `FunctionsApplication.CreateBuilder(args)` | `CreateBuilder` 内で即時実行 | 必ず後 |
| `new HostBuilder().ConfigureFunctionsWebApplication().ConfigureServices(...)` | `Build()` 時にコールバック順で実行 | `ConfigureServices` を `ConfigureFunctionsWebApplication` より後に書けば後 |

通常の書き方であれば、`AddAzureFunctionExtension()` の登録は Worker SDK の自動登録より後になる (両方式とも実ホストで確認)。
`IAutoConfigureStartup` 同士の実行順は `GetTypes()` の列挙順に依存するため、順序に依存する登録には使えない。

### 3.6 ソリューション単位の復元と `AZFW0108`

新SDKはホスト拡張の解決用プロジェクトを `AfterTargets="Restore"` のターゲットで生成・復元する。
ソリューション (`.slnx`) 単位の復元ではプロジェクトの `Restore` ターゲットが実行されないため、この処理が動かず、
ビルド時の代替処理に切り替わって `AZFW0108` 警告が出る (成果物は正しく生成される)。

| 操作 (Functions プロジェクトの `obj` が無い状態から) | `AZFW0108` |
| --- | --- |
| `dotnet build AzureFunctionsExtension.slnx` | 出る |
| `dotnet restore AzureFunctionsExtension.slnx` → `dotnet build AzureFunctionsExtension.slnx --no-restore` | 出る |
| `dotnet build AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj` | 出ない |
| `dotnet restore AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj` → `dotnet build AzureFunctionsExtension.slnx` | 出ない |
| 2 回目以降の `dotnet build AzureFunctionsExtension.slnx` | 出ない |
| Debug でビルド後に `-c Release` でソリューションビルド | 出る (このリポジトリでは Release のみ SourceLink を参照するため `project.assets.json` が変わる) |

- 判定は `obj/azure_functions/restored.marker` と `project.assets.json` の新旧比較。`project.assets.json` が書き換わると再発する。
- Visual Studio 内のビルドでは SDK 側で警告が抑止される (`BuildingInsideVisualStudio`)。
- 対処は「CI などでは Functions プロジェクトを先にプロジェクト単位で復元する」とする (9.1)。警告の抑制は行わない (AGENTS.md の方針)。
  このリポジトリでは構成によって参照パッケージが変わるため、構成も合わせて復元する
  (`dotnet restore AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj -p:Configuration=Release` →
  `dotnet build AzureFunctionsExtension.slnx -c Release` で警告 0 を確認)。

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

## 5. 対応方針 (確定版)

### 5.1 全体像

Worker SDK のジェネレーターの出力 (手書き `[Function]` 用) はそのまま活かし、本ライブラリが生成した関数を
**メタデータは Transformer で追加**、**実行は Executor のデコレーターで横取り** する。登録の誤りは **起動時検証** で検出する。

```
[生成コード (アプリのアセンブリ)]
  FunctionCatalog : IGeneratedFunctionCatalog
    ├─ [ModuleInitializer] Initialize() ──► GeneratedFunctionCatalog.Register(...)   (静的レジストリ)
    ├─ AddMetadata(list)      … [AzureFunction] の関数のメタデータ
    └─ TryExecuteAsync(ctx)   … EntryPoint で振り分けてスタブ (Xxx_Handler) を直接呼び出す
  FunctionCatalogAutoStartup : IAutoConfigureStartup ──► GeneratedFunctionCatalog.ConfigureStartup(...)

[AddAzureFunctionExtension() が登録]
  IFunctionMetadataTransformer = ExtensionFunctionMetadataTransformer
      └─ レジストリのカタログからメタデータを追加 (既に存在する EntryPoint は追加しない)
  IFunctionExecutor = ExtensionFunctionExecutor (デコレーター)
      ├─ カタログが処理できる EntryPoint → 生成コードで直接呼び出し
      └─ それ以外 → 元の Executor (Worker SDK 生成の DirectFunctionExecutor) へ委譲

[FunctionCatalogAutoStartup が登録 (登録順序に依存しない)]
  IHostedService = ExtensionStartupValidator
      └─ 起動時に「AddAzureFunctionExtension() が呼ばれているか」「デコレーターが Executor チェーンに含まれるか」を検証
```

- メタデータ・実行の両方をジェネレーターが静的に出力するため、リフレクションに依存しない (Worker SDK の生成コードと同等)。
- 手書きの `[Function]` との混在、他ライブラリの Transformer との共存が可能 (実ホストで混在を確認)。
- 既存の `[Function]` スタブ (`Xxx_Handler`) は **エントリーポイントとして残す** (生成内容は変更しない)。

### 5.2 ジェネレーターが追加で出力するコード

アプリケーションのアセンブリに 1 ファイル (`AzureFunctionsExtension.Generated.FunctionCatalog.g.cs`) を出力する。
以下はパッケージ参照で検証した `MyApp` (HTTP 関数 1 件) の実際の出力。Example (10 関数) では 252 行になる。

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
        var bindings0 = new global::System.Collections.Generic.List<string>(2);
        bindings0.Add("{\"name\":\"req\",\"type\":\"httpTrigger\",\"direction\":\"In\",\"authLevel\":\"Anonymous\",\"methods\":[\"get\"],\"route\":\"hello/{name}\"}");
        bindings0.Add("{\"name\":\"$return\",\"type\":\"http\",\"direction\":\"Out\"}");
        metadata.Add(new global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.DefaultFunctionMetadata
        {
            Language = "dotnet-isolated",
            Name = "Hello",
            EntryPoint = "MyApp.SampleFunction.Hello_Handler",
            ScriptFile = ScriptFile,
            RawBindings = bindings0,
        });
    }

    public global::System.Threading.Tasks.ValueTask<bool> TryExecuteAsync(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        switch (context.FunctionDefinition.EntryPoint)
        {
            case "MyApp.SampleFunction.Hello_Handler":
                return __Execute0__(context);
            default:
                return new global::System.Threading.Tasks.ValueTask<bool>(false);
        }
    }

    private static async global::System.Threading.Tasks.ValueTask<bool> __Execute0__(global::Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        var values = await __BindAsync__(context);
        global::Microsoft.Azure.Functions.Worker.FunctionContextBindingFeatureExtensions.GetInvocationResult(context).Value = await global::MyApp.SampleFunction.Hello_Handler((global::Microsoft.AspNetCore.Http.HttpRequest)values[0]!, context);
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

internal sealed class FunctionCatalogAutoStartup : global::Microsoft.Azure.Functions.Worker.IAutoConfigureStartup
{
    public void Configure(global::Microsoft.Extensions.Hosting.IHostBuilder hostBuilder)
    {
        global::AzureFunctionsExtension.Hosting.GeneratedFunctionCatalog.ConfigureStartup(hostBuilder);
    }
}
```

| 項目 | 規則 |
| --- | --- |
| `Name` | `handler.MethodName` (スタブの `[Function]` と同じ) |
| `EntryPoint` | `FunctionModel.MetadataName + "." + MethodName + "_Handler"`。エスケープ前のメタデータ名 (`namespace @class;` → `class`)。`ToDisplayString()` は `@class` を返すため使わない (実測) |
| `ScriptFile` | `Compilation.AssemblyName + ".dll"` (Worker SDK の生成コードと同じ規則) を定数として 1 回だけ出力 |
| `RawBindings` | 5.3 の JSON をジェネレーター内でエスケープして組み立て、`CSharpLiteral.Format` でリテラル化 |
| 並び順 | モデルの順 → ハンドラーの順。`__ExecuteN__` の N はカタログ全体の通し番号 |
| ディスパッチ | HTTP: `(HttpRequest)values[0]` を渡し、結果を `GetInvocationResult(context).Value` へ設定。Timer: `(TimerInfo)values[0]`。Queue: `(string)values[0]` |
| `TryExecuteAsync` | 非同期メソッドにせず、一致しない場合は同期的に `false` を返す (他の関数の呼び出しで状態機械を作らない) |
| 出力条件 | スタブを生成するクラス (エラーやヒント名衝突のあるクラスを除く) と同じ集合。ハンドラーが 1 件も無ければ出力しない |
| 言語機能 | 利用者の `LangVersion` に依存しないよう、コレクション式や raw 文字列リテラルは使わない |

### 5.3 バインディング JSON

Worker SDK のジェネレーターが同じシグネチャの手書き関数に出力した JSON を基準にした (実測)。実ホストで HTTP / Timer / Queue とも動作確認済み。

| トリガー | `RawBindings` |
| --- | --- |
| HTTP | `{"name":"req","type":"httpTrigger","direction":"In","authLevel":"<AuthorizationLevel>","methods":["<method>"],"route":"<route>"}`<br>`{"name":"$return","type":"http","direction":"Out"}` |
| Timer | `{"name":"timerInfo","type":"timerTrigger","direction":"In","schedule":"<NCRONTAB>"}` |
| Queue | `{"name":"message","type":"queueTrigger","direction":"In","queueName":"<queue>","connection":"<connection>","dataType":"String"}` (`connection` は指定時のみ) |

- `name` は既存スタブのパラメーター名 (`req` / `timerInfo` / `message`) と一致させる。
- `authLevel` は実ホストで有効 (`AuthorizationLevel.Function` の関数はキー無しで 401、キー付きでは関数が実行される。Example の該当関数は既存不具合で 500 になる。8 章)。
- HTTP メソッドは属性の値をそのまま出力する (現行の `functions.metadata` と同じ)。

### 5.4 ランタイムライブラリ側

`AzureFunctionsExtension/Hosting/` に次の型を追加する (全文は付録 A)。リポジトリの解析設定 (`AnalysisMode=All`、StyleCop、`IsAotCompatible`) で警告 0 を確認済み。

| 型 | 公開範囲 | 役割 |
| --- | --- | --- |
| `IGeneratedFunctionCatalog` | public (`EditorBrowsable(Never)`) | 生成コードが実装する。`AddMetadata` / `TryExecuteAsync` |
| `GeneratedFunctionCatalog` | public static (`EditorBrowsable(Never)`) | カタログの静的レジストリ (`Register`)。起動時検証の登録 (`ConfigureStartup`) |
| `ExtensionFunctionMetadataTransformer` | internal | レジストリのカタログからメタデータを追加。既に存在する EntryPoint は追加しない |
| `ExtensionFunctionExecutor` | internal | デコレーター。カタログで処理できなければ内側の Executor へ委譲、内側も無ければ例外 |
| `ExtensionFunctionExecutorState` | internal | デコレーターが生成されたか (Executor チェーンに含まれるか) を記録 |
| `ExtensionStartupValidator` | internal (`IHostedService`) | 起動時に登録漏れ・登録順序の誤りを検出して例外 |

`AddAzureFunctionExtension()` での登録 (既存の 2 つのオーバーロードの両方から呼ぶ):

```csharp
// Registers the metadata transformer and wraps the current function executor (the one generated by the Worker SDK)
private static void AddFunctionCatalog(IServiceCollection services)
{
    if (services.Any(static x => x.ServiceType == typeof(ExtensionFunctionExecutorState)))
    {
        return;
    }

    var previous = services.LastOrDefault(static x => (x.ServiceType == typeof(IFunctionExecutor)) && !x.IsKeyedService);
    services.AddSingleton(static _ => new ExtensionFunctionExecutorState());
    services.AddSingleton<IFunctionMetadataTransformer>(static _ => new ExtensionFunctionMetadataTransformer());
    services.AddSingleton<IFunctionExecutor>(sp => new ExtensionFunctionExecutor(
        previous is null ? null : ResolveExecutor(sp, previous),
        sp.GetRequiredService<ExtensionFunctionExecutorState>()));
}

private static IFunctionExecutor ResolveExecutor(IServiceProvider serviceProvider, ServiceDescriptor descriptor) =>
    (IFunctionExecutor)(descriptor.ImplementationInstance ??
        descriptor.ImplementationFactory?.Invoke(serviceProvider) ??
        ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!));
```

設計上の要点:

- **重複登録の防止**: 複数回呼ばれても二重登録しない (Transformer が 2 つになると関数とルートが重複する)。
- **重複メタデータの除外**: Transformer は、プロバイダーが既に返している EntryPoint を追加しない。
  これが無いと、`functions.metadata` を持つ構成 (旧SDK) では HTTP ルートが重複してリクエストが関数に届かなくなる (実測。7.3)。
- **キー付き登録の除外**: キー付き登録の `ImplementationType` は参照すると例外になるため、`IsKeyedService` を除外する。
- **AOT/トリミング**: `ServiceDescriptor.ImplementationType` は `DynamicallyAccessedMembers(PublicConstructors)` 注釈付きのため警告は出ない。
  内部型はファクトリーラムダで生成する (`AddSingleton<TService, TImplementation>()` だと `CA1812` の対象になり得るため)。
- **カタログの読み取りは使用時**: `AddAzureFunctionExtension()` の時点では、テストなどでモジュール初期化子がまだ実行されていない場合があるため。
  テスト用に Transformer / Executor はカタログを直接受け取る引数を持つ (`InternalsVisibleTo` でテストから使う)。
- 依存する Worker の最低バージョンは 2.1.0 (`IFunctionMetadataTransformer`)。現行の参照 (2.52.0) で満たしている。

### 5.5 登録方法と起動時検証

| 対象 | 方法 | 理由 |
| --- | --- | --- |
| カタログ | `[ModuleInitializer]` で静的レジストリへ登録 | リフレクション不要。利用者の追加手順が不要 |
| Transformer / Executor デコレーター | `AddAzureFunctionExtension()` で登録 | Executor は「最後の登録」である必要があり、Worker SDK の自動登録より後に確実に実行できるのは利用者の呼び出しだけ |
| 起動時検証 | 生成コードの `IAutoConfigureStartup` から `IHostedService` を登録 | `IHostedService` は複数登録でき順序に依存しないため、自動登録で問題ない。`AddAzureFunctionExtension()` の呼び忘れも検出できる |

当初案 (Transformer 内で検証して例外) は実ホストで次の問題があったため廃止した。

| 誤り | Transformer 内で検証 (当初案) | 起動時検証 (確定) |
| --- | --- | --- |
| 登録順序が逆 (`ConfigureServices` → `ConfigureFunctionsWebApplication`) | 例外はホストのログに出ず、**手書きの関数も含めて全関数が消える** (`No job functions found.`) | ワーカーが起動せず、ホストのログに `Unhandled exception. System.InvalidOperationException: The function executor of AzureFunctionsExtension is not used. Call AddAzureFunctionExtension() after ConfigureFunctionsWebApplication().` |
| `AddAzureFunctionExtension()` の呼び忘れ | 検出できず、**生成した関数だけが無言で消える** | `Unhandled exception. System.InvalidOperationException: AzureFunctionsExtension is not registered. Call AddAzureFunctionExtension().` |

### 5.6 MSBuild (`AzureFunctionsExtension.props` / `.targets`)

- 削除: `FunctionsEnableWorkerIndexing` (`AZFW0110` の原因)、`FunctionsEnableExecutorSourceGen` (SDK に上書きされ効果が無い)。
- 追加: `<CompilerVisibleProperty Include="UsingAzureFunctionsSdk" />` (`Sdk.props` で `true` が設定される)。
  `FunctionsEnableMetadataSourceGen` などは SDK 側で既に `CompilerVisibleProperty` に登録済み。
- `AzureFunctionsExtension.targets` は空のまま。Worker の Executor ソース生成は有効のままでよい (デコレーターで包むため)。

### 5.7 診断

| ID | 変更 | 内容 | 確認 |
| --- | --- | --- | --- |
| AFE0601 | 置き換え | `build_property.UsingAzureFunctionsSdk` が `true` でない (新SDKを使っていない) 場合にエラー | 旧SDK構成のパッケージ利用者でエラーになることを確認 |
| AFE0602 | 追加 | `FunctionsEnableMetadataSourceGen` / `FunctionsAutoRegisterGeneratedMetadataProvider` が無効な場合にエラー (既定プロバイダーが `functions.metadata` を要求して起動に失敗するため) | `FunctionsEnableMetadataSourceGen=false` でエラーになることを確認 |
| AFE0205 | 拡張 | 同じコンパイル内の手書き `[Function]` と関数名が重複 (大文字小文字を区別しない) する場合もエラー | 重複時に生成した関数が無言で消えることを実ホストで確認したうえで、ビルドエラーになることを確認 |

- `AFE0601` / `AFE0602` は `[AzureFunction]` クラスが 1 つ以上ある場合のみ報告し、`AFE0601` を優先する。
- `[AzureFunction]` をクラスライブラリに置いた場合も `AFE0601` になる (モジュール初期化子がメタデータ要求時点で実行されている保証が無いため、意図的に対象外とする)。

---

## 6. 検討した代替案

| 案 | 内容 | 不採用の理由 |
| --- | --- | --- |
| A | Worker のメタデータ生成を無効化し、本ライブラリの MSBuild タスクで `functions.metadata` を生成する | Mono.Cecil などでアセンブリを走査するタスクの実装・配布が必要。旧SDKの仕組みの再実装になる |
| B | 利用者に `[Function]` + トリガー属性付きの `partial` メソッド宣言を書かせ、本体をジェネレーターで実装する | Worker SDK のジェネレーターから見えるようになるが、属性ベースの簡潔な記述というライブラリの価値が失われる |
| C | `IFunctionMetadataProvider` を丸ごと置き換える | 手書きの `[Function]` や他ライブラリのメタデータが失われる。公開拡張ポイントの Transformer で足りる |
| D | Executor ではなくミドルウェアで実行を横取りする | 利用者のミドルウェアとの前後関係に依存し、後から登録されたミドルウェアが適用されない |
| E | Executor のソース生成を `.targets` で無効化し、Worker 既定のリフレクション Executor に任せる | 動作はするが、リフレクション呼び出しになり AOT/トリミングに不利。メタデータ側の対応は別途必要 |
| F | Executor デコレーターも生成コードの `IAutoConfigureStartup` から登録する | Worker SDK の Executor 登録との前後関係が `GetTypes()` の列挙順に依存し、保証されない |

---

## 7. 検証内容

### 7.1 環境

| 項目 | 内容 |
| --- | --- |
| .NET SDK | 10.0.112 (Ubuntu パッケージ)。ジェネレーターが `Microsoft.CodeAnalysis.CSharp` 5.9.0 を参照しているため、SDK 同梱の Roslyn 5.0 では読み込まれない (`CS9057`)。検証用コピーに `Microsoft.Net.Compilers.Toolset` 5.9.0 を追加した (新SDKとは無関係の既存の前提) |
| 逆コンパイル | `Azure.Functions.Sdk` 1.0.1 (props / targets / タスク DLL)、Worker 2.52.0 (`Core` / `Grpc` / `Worker`)、`Worker.Extensions.Http.AspNetCore` 2.1.1 |
| 実装 | リポジトリの HEAD をスクラッチ環境に展開し、9 章のフェーズ 1〜6 を実装 (28 ファイル、+1,654 / -78 行) |
| E2E | Docker で Functions Host コンテナー (`mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0`、Host 4.1054.250.6) と Azurite を起動し、発行した Example / 検証用アプリを実行 (手順は付録 B) |

### 7.2 調査段階の検証

| # | 内容 | 結果 |
| --- | --- | --- |
| 1 | 現行構成 (旧SDK) で Example をビルド | 警告 0。`functions.metadata` に 10 関数、`worker.config.json` は `workerIndexing: false` |
| 2 | 手書き `[Function]` (HTTP / Timer / Queue) を新SDKでビルド | `functions.metadata` 無し、`workerIndexing: true`。Worker SDK 生成コードからバインディング JSON とディスパッチ形式を採取 |
| 3 | Example を新SDKに変更してビルド | `AZFW0110` + `AFE0601` |
| 4 | 3 の Worker 生成コードを確認 | メタデータ 0 件、Executor は空 |
| 5 | メタデータのソース生成を無効化して起動 | `FileNotFoundException: Function metadata file not found.` |
| 6 | csproj にのみ SDK バージョンを指定して並列ビルド | `azure_functions.g.csproj` で `MSB4236`。`-m:1` は成功、`global.json` 固定で解消 |
| 7 | Example.Tests を新SDK版 Example に対して実行 | 15 件成功 (Exe の Functions プロジェクトを参照するテストプロジェクトも問題なし) |

### 7.3 フェーズごとの実装検証

| フェーズ | ビルド | テスト | その他の確認 |
| --- | --- | --- | --- |
| 1 ランタイム | 警告 0 (Release、AOT 解析を含む) | 92 件成功 | — |
| 2 ジェネレーター下準備 | 警告 0 | 92 件成功 | Example の生成コードが変更前と **完全に一致** (`diff -r`) |
| 3 カタログ生成 | 警告 0 | 105 件成功 | 既存の生成ファイルは不変で、カタログ 1 ファイルのみ追加 |
| 3 (旧SDKのまま E2E) | — | — | 重複除外が無い状態では HTTP 関数が全て 500 (`Timed out waiting for the function start call`)。原因は `functions.metadata` とカタログの両方から同じ関数が返りルートが重複したこと。重複除外の追加後は **ベースラインと完全一致** |
| 4 新SDKへの切り替え | 警告 0 (2 回目以降。初回は 3.6 の `AZFW0108`) | 109 件 + 15 件成功 | 出力に `worker.config.json` (`workerIndexing: true`)、`extensions.json`、`host.json` があり `functions.metadata` が無い |
| 5 Example.Tests 結合テスト | 警告 0 | 109 件 + 21 件成功 | — |
| 6 起動時検証・AFE0205 拡張 | 警告 0 (Debug / Release) | **114 件 + 21 件成功** | 実ホストで異常系を確認 (7.5) |

### 7.4 E2E 結果 (現行の旧SDK構成 と 新SDK 実装後の比較)

同じチェックスクリプトの出力を `diff` で比較し、**完全に一致** した。

| # | 呼び出し | 結果 (両構成で同一) |
| --- | --- | --- |
| 1 | `GET /api/ping` | 200 `{"message":"pong"}` |
| 2 | `GET /api/query?a=1&b=2` | 200 `{"result":6}` |
| 3 | `GET /api/query?a=1` | 200 `{"result":4}` (既定値 `c=3`) |
| 4 | `GET /api/query?a=x` | 400 `Invalid parameter: a` |
| 5 | `GET /api/array?a=1,2&b=3,` | 400 `Invalid parameter: b` |
| 6 | `GET /api/items/5` | 200 `{"id":5,"name":"Item-5"}` |
| 7 | `GET /api/items/0` | 404 `Item 0 not found.` |
| 8 | `GET /api/items/abc` | 400 `Invalid parameter: id` |
| 9 | `GET /api/header-echo` (`X-Correlation-Id: abc`) | 200 `{"correlationId":"abc"}` |
| 10 | `GET /api/header-echo` | 200 `{"correlationId":"(none)"}` |
| 11 | `GET /api/items/foo/lookup` | 200 `{"id":0,"name":"foo"}` |
| 12 | `GET /api/items/missing/lookup` | 200 `{"id":0,"name":"missing"}` |
| 13 | `POST /api/greet` | 500 (既存不具合。8 章) |
| 14 | `POST /api/body` (キー無し) | 401 (`authLevel: Function` が有効) |
| 15 | `POST /api/body` (関数キー付き) | 500 (既存不具合。8 章) |
| 16 | `GET /api/ping` (`X-HTTP-Method-Override: POST`) | 200 |
| 17 | `POST /api/ping` | 404 (メソッド制約が有効) |
| 18 | `GET /api/notfound` | 404 |
| 19 | 関数一覧 (ホストのログ) | 10 件 (`Array`、`Body`、`GetItem`、`Greet`、`HeaderEcho`、`Lookup`、`Ping`、`ProcessMessage`、`Query`、`Timer`) |
| 20 | `my-queue` へメッセージ送信 | `ProcessMessage` 成功、`[FromServices]` の結果をログ出力 |
| 21 | 管理 API で `Timer` を実行 (`POST /admin/functions/Timer`) | 202、`Timer` 成功 |

### 7.5 パッケージ参照・混在・異常系

ローカルフィードに作成したパッケージを、リポジトリ外の新SDKプロジェクト (`TreatWarningsAsErrors` 有効) から参照して確認した。

| # | 構成 | 結果 |
| --- | --- | --- |
| 1 | 新SDK + パッケージ参照 | 警告 0。`AZFW0110` / `AFE0601` 無し (`.props` 経由の `CompilerVisibleProperty` が有効)。カタログが生成される |
| 2 | 1 に手書きの `[Function("Plain")]` を追加 | 実ホストで `Hello` (生成) と `Plain` (手書き) の両方が一覧に出て、両方とも 200 |
| 3 | `HostBuilder` 方式 (`ConfigureFunctionsWebApplication` → `ConfigureServices`) | 実ホストで動作 |
| 4 | `HostBuilder` 方式で順序が逆 | 起動時検証で原因付きの起動失敗 (5.5) |
| 5 | `AddAzureFunctionExtension()` を呼ばない | 起動時検証で原因付きの起動失敗 (5.5) |
| 6 | 手書きの `[Function("Hello")]` と生成した `Hello` が重複 | 対策前: ホストは手書きの方だけを登録し、生成した関数は無言で消えた。対策後: `AFE0205` でビルドエラー |
| 7 | `Microsoft.NET.Sdk` + `Microsoft.Azure.Functions.Worker.Sdk` 2.1.0 (旧SDK) | `AFE0601` でビルドエラー |
| 8 | 新SDK + `FunctionsEnableMetadataSourceGen=false` | `AFE0602` でビルドエラー |
| 9 | `dotnet pack` | `lib/net10.0/AzureFunctionsExtension.dll`、`analyzers/dotnet/cs/AzureFunctionsExtension.Generator.dll`、新しい `build/AzureFunctionsExtension.props` を含む |

### 7.6 未検証事項

- Azure へのデプロイ (Flex Consumption / Premium / App Service)。コンテナーの Functions Host では確認済み。
- Native AOT 発行 (Functions の isolated worker での AOT 自体の対応状況を含めて未確認)。
- Windows / Visual Studio でのビルドとデバッグ (`dotnet run` による Core Tools 起動を含む)。
- 他のトリガー (Service Bus など) は本ライブラリの対象外のため未確認。

---

## 8. リスク・留意点

- `Azure.Functions.Sdk` は 1.0 系になったばかりで、プロパティやターゲットの変更が起こり得る。
- 本方式は、デコレーターが `IFunctionExecutor` の最後の登録になることに依存する。誤った場合は起動時検証で検出する (5.5)。
- Worker SDK が将来、生成コードに対する公式の連携手段を提供した場合は、そちらへの移行を検討する。
- `AddAzureFunctionExtension()` は必須になる (呼ばないと起動に失敗する)。README での明記が必要。
- ソリューション単位のクリーンビルドで `AZFW0108` が出る (3.6)。CI では Functions プロジェクトを先にプロジェクト単位で復元する。
- **既存不具合 (新SDKとは無関係)**: `[FromBody]` を使う HTTP 関数は実ホストで常に 500 になる。
  - 原因: 生成コードが `IBodySerializer.Deserialize<T>(req.Body)` を同期で呼び、`JsonSerializer.Deserialize(Stream)` が Kestrel の本文を同期で読むため
    (`InvalidOperationException: Synchronous operations are disallowed.`)。`JsonException` 以外は 500 になる。
  - Example.Tests は `MemoryStream` の本文を使うため検出できていない。
  - 対応案: `IBodySerializer` に非同期の読み取り (`DeserializeAsync`) を追加して生成コードで `await` する。同期読み取りを禁止するストリームでの回帰テストを追加する。
- in-process モデルのサポートは 2026-11-10 で終了予定で、isolated worker + 新SDK が今後の標準になる。

---

## 9. 実装プラン (検証済み)

各フェーズの内容はスクラッチ環境で実装・検証済み。コードの全文は付録 A を参照。

### 9.1 決定事項

| # | 事項 | 決定 |
| --- | --- | --- |
| 1 | `AFE0601` の意味を変えて再利用するか | 再利用する (ベータ期間中のため。`Diagnostics.md` で明記) |
| 2 | スタブの `[Function]` 属性を残すか | 残す (生成内容を変えない) |
| 3 | `global.json` に `"test": { "runner": "Microsoft.Testing.Platform" }` を追加するか | 本対応とは分ける |
| 4 | パッケージバージョン | メンテナーの判断 (例: 2.0.0-beta10) |
| 5 | `AFE0205` の拡張 | 当初は後続扱いだったが、検証で「生成した関数が無言で消える」ことが分かったため、**今回含める** (フェーズ 6) |
| 6 | 登録漏れ・登録順序の誤りの検出 | 生成コードの `IAutoConfigureStartup` による起動時検証とする (5.5) |
| 7 | `AZFW0108` の扱い | 抑制しない。CI では Functions プロジェクトを先に復元する (3.6) |
| 8 | `[FromBody]` の既存不具合 | 本対応とは別に対応する (8 章) |

### 9.2 フェーズ一覧

| フェーズ | 内容 | 主な成果物 | 実測の規模 | 状態 |
| --- | --- | --- | --- | --- |
| 1 | ランタイムライブラリ | `AzureFunctionsExtension/Hosting/*`、`ServiceCollectionExtensions.cs`、`Assembly.cs` | 本体 約 170 行、テスト 約 310 行 | 検証済み |
| 2 | ジェネレーターの下準備 (出力変更なし) | `FunctionModel.MetadataName`、`HandlerModelExtensions` | 約 50 行 | 検証済み |
| 3 | カタログ生成 | `JsonText`、`BindingJsonBuilder`、`FunctionCatalogSourceBuilder`、`FunctionGenerator` | 本体 約 340 行、テスト 約 380 行 | 検証済み |
| 4 | 新SDKへの切り替え | 診断 (`AFE0601`/`AFE0602`)、`AzureFunctionsExtension.props`、Example の csproj / `global.json` / Program.cs | 約 170 行 | 検証済み |
| 5 | Example.Tests の結合テスト | `FunctionCatalogTests.cs`、`FakeFunctionContext` 拡張 | 約 180 行 | 検証済み |
| 6 | 起動時検証の生成と `AFE0205` の拡張 | `ExtensionStartupValidator`、`FunctionCatalogAutoStartup` の出力、`FunctionGenerator` | 約 220 行 (テスト含む) | 検証済み |
| 7 | ドキュメント | `README.md`、`Diagnostics.md` | — | 未着手 |
| 8 | 総合検証 | — | — | 9.10 |

- 各フェーズ完了時点で、ビルド警告 0・全テスト成功を保てることを確認済み。
  フェーズ 3 で Transformer の重複除外を入れておくことで、フェーズ 4 の前 (旧SDKのまま) でも E2E がベースラインと一致する。
- フェーズ 4 は分割しない (旧 `AFE0601` は新SDKで、新 `AFE0601` は旧SDKの Example で必ずエラーになるため)。
- フェーズ 1 の Transformer 内検証は採用せず、起動時検証はフェーズ 6 で追加する (フェーズ 6 をフェーズ 1・3 に統合してもよい)。

### 9.3 フェーズ 1: ランタイムライブラリ

| ファイル | 種別 | 内容 |
| --- | --- | --- |
| `AzureFunctionsExtension/Hosting/IGeneratedFunctionCatalog.cs` | 新規 | 付録 A |
| `AzureFunctionsExtension/Hosting/GeneratedFunctionCatalog.cs` | 新規 | 付録 A (`ConfigureStartup` はフェーズ 6) |
| `AzureFunctionsExtension/Hosting/ExtensionFunctionExecutorState.cs` | 新規 | 付録 A |
| `AzureFunctionsExtension/Hosting/ExtensionFunctionMetadataTransformer.cs` | 新規 | 付録 A (重複除外を含む) |
| `AzureFunctionsExtension/Hosting/ExtensionFunctionExecutor.cs` | 新規 | 付録 A |
| `AzureFunctionsExtension/ServiceCollectionExtensions.cs` | 変更 | `AddFunctionCatalog(services)` を追加 (5.4) |
| `AzureFunctionsExtension/Assembly.cs` | 変更 | `[assembly: InternalsVisibleTo("AzureFunctionsExtension.Tests")]` |

テスト `AzureFunctionsExtension.Tests/HostingTests.cs` (新規。カタログ・Executor・`FunctionContext` は最小限のフェイク):

| テスト | 確認内容 |
| --- | --- |
| `TransformerAddsMetadataOfCatalogsInOrder` | 複数カタログの登録順に追加され、元の要素が残る |
| `TransformerSkipsFunctionAlreadyProvided` | 既に存在する EntryPoint は追加しない |
| `ExecutorUsesCatalogWithoutCallingInner` | カタログが処理したら内側を呼ばない |
| `ExecutorDelegatesToInnerWhenNoCatalogHandles` | どのカタログも処理しなければ内側へ委譲 |
| `ExecutorThrowsWhenNoInnerAndNoCatalogHandles` | 内側も無ければ `InvalidOperationException` (EntryPoint を含む) |
| `RegistrationWrapsPreviousExecutor` (Theory 3 件) / `RegistrationWrapsPreviousExecutorRegisteredByType` | 型 / ファクトリー / インスタンス登録の Executor を包む |
| `RegistrationIsIdempotent` | 2 回呼んでも Transformer 1 件、Executor は二重に包まれない |
| `RegistrationIgnoresKeyedExecutor` | キー付き登録は対象外で例外も出ない |
| `RegistryIgnoresSameCatalogType` | 同じ型のカタログは 1 回だけ登録 (静的状態のため専用のフェイク型を使う) |

### 9.4 フェーズ 2: ジェネレーターの下準備 (出力変更なし)

| ファイル | 内容 |
| --- | --- |
| `Models/FunctionModel.cs` | `string MetadataName` を `ClassName` の後に追加 |
| `FunctionModelBuilder.cs` | `GetMetadataName(symbol)` (名前空間とクラスの `MetadataName` を連結) を追加して設定 |
| `Models/HandlerModelExtensions.cs` (新規) | `GetHttpMethod` / `GetRoute` / `GetAuthorizationLevel` / `GetTimerSchedule` / `GetQueueName` / `GetEntryPointMethodName` |
| `FunctionSourceBuilder.cs` | `?? "get"` などの既定値処理と `_Handler` の付与を拡張メソッド経由に置き換え |

完了条件: Example の生成コードが変更前と完全に一致し (`EmitCompilerGeneratedFiles` で出力して `diff -r`)、既存テストが無変更で成功する (確認済み)。

### 9.5 フェーズ 3: カタログ生成

| ファイル | 種別 | 内容 |
| --- | --- | --- |
| `AzureFunctionsExtension.Generator/JsonText.cs` | 新規 | JSON 文字列のエスケープと、プロパティ / 配列プロパティの書き込み (付録 A) |
| `AzureFunctionsExtension.Generator/BindingJsonBuilder.cs` | 新規 | 5.3 の JSON を生成。パラメーター名の定数 (`req` / `timerInfo` / `message`) を持つ (付録 A) |
| `AzureFunctionsExtension.Generator/FunctionCatalogSourceBuilder.cs` | 新規 | 5.2 のコードを出力 (付録 A) |
| `AzureFunctionsExtension.Generator/FunctionGenerator.cs` | 変更 | カタログのパイプラインと `ExecuteCatalog` を追加 (付録 A) |

```csharp
// FunctionGenerator.Initialize: 既存の collected (ImmutableArray<Result<FunctionModel>>) を再利用する
var assemblyNameProvider = context.CompilationProvider
    .Select(static (compilation, _) => compilation.AssemblyName ?? string.Empty);
var catalogProvider = collected
    .Select(static (results, _) => new EquatableArray<FunctionModel>(SelectModels(results).ToArray()))
    .Combine(assemblyNameProvider);
context.RegisterImplementationSourceOutput(catalogProvider, static (ctx, input) => ExecuteCatalog(ctx, input.Left, input.Right));
```

- `CompilationProvider` は無関係な編集でも再評価されるが、`Select` の結果 (アセンブリ名) が同じなら以降はキャッシュされる
  (既存の `PipelineCacheTests` がカタログ出力も含めて成功することを確認)。
- ヒント名は `AzureFunctionsExtension.Generated.FunctionCatalog.g.cs` 固定 (既存のクラス単位のヒント名は `_` 区切りのため衝突しない)。

テスト `AzureFunctionsExtension.Tests/CatalogTests.cs` (新規) と `CompilationHelper.RunGenerator(source, assemblyName)` (追加):

| テスト | 確認内容 |
| --- | --- |
| `GeneratesHttpMetadata` | 認可レベル既定 (`Function`) と指定時 (`Anonymous`)、ルート、`$return` の http 出力 |
| `GeneratesTimerMetadata` | `schedule`、バインディング 1 件 |
| `GeneratesQueueMetadataWithAndWithoutConnection` | `connection` の有無、`dataType: String` |
| `EscapesBindingJson` | `"`、`\`、タブ、制御文字を含むルートを `JsonDocument.Parse` で往復確認 |
| `BindingJsonIsParsableForAllTriggers` | 全トリガーの JSON が解析でき `name` / `type` / `direction` を持つ |
| `UsesAssemblyNameForScriptFile` | `WithAssemblyName("MyApp")` で `"MyApp.dll"` |
| `UsesMetadataNameForEntryPoint` | `namespace @class;` / `@event` / `@default` で `class.event.default_Handler`、呼び出しは `global::@class.@event.default_Handler(` |
| `UsesMetadataNameForEntryPointInGlobalNamespace` | グローバル名前空間で `GlobalFunction.Run_Handler` |
| `GeneratesDispatchForEachHandler` | ハンドラーごとの `case`、トリガーごとのキャスト、`GetInvocationResult` は HTTP のみ |
| `DoesNotGenerateCatalogWithoutHandlers` | ハンドラーが無ければ出力しない |
| `DoesNotIncludeClassWithError` | エラー (`AFE0001`) のクラスは含めない |
| `DoesNotIncludeHintNameCollision` | `AFE0501` で生成対象外のクラスは含めない |
| `GeneratedCatalogCompiles` | 生成コードを含めてコンパイル問題 (生成ファイルの警告を含む) が無い |

### 9.6 フェーズ 4: 新SDKへの切り替え

| ファイル | 内容 |
| --- | --- |
| `AzureFunctionsExtension.Generator/Diagnostics.cs` | `WorkerSourceGenEnabled` を削除し、`AzureFunctionsSdkRequired` (AFE0601) と `WorkerMetadataSourceGenDisabled` (AFE0602) を追加 (付録 A) |
| `AzureFunctionsExtension.Generator/Models/BuildConfigModel.cs` (新規) | `record BuildConfigModel(bool UsingAzureFunctionsSdk, bool MetadataSourceGenEnabled)` |
| `AzureFunctionsExtension.Generator/FunctionGenerator.cs` | `IsWorkerSourceGenEnabled` / `ReportWorkerSourceGen` を `ReportBuildConfiguration` に置き換え (付録 A) |
| `AzureFunctionsExtension.props` | 5.6 の通り (付録 A) |
| `global.json` (新規) | `msbuild-sdks` で `Azure.Functions.Sdk` 1.0.1 を固定 |
| `AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj` | `Sdk="Azure.Functions.Sdk"`。`AzureFunctionsVersion` / `OutputType` / `Microsoft.Azure.Functions.Worker.Sdk` / `host.json` 等のコピー指定を削除 (付録 A) |
| `AzureFunctionsExtension.Example/Program.cs` | `FunctionsApplication.CreateBuilder(args)` 形式 (付録 A)。トップレベルに `await` を置くと `Run()` に `CA1849` が出るため置かない |
| `AzureFunctionsExtension.Tests/CompilationHelper.cs` | `Runner` に `UsingAzureFunctionsSdk` / `FunctionsEnableMetadataSourceGen` / `FunctionsAutoRegisterGeneratedMetadataProvider` = `true` の既定オプションを追加 (`WithGlobalOption` は同じキーを上書きできる) |
| `AzureFunctionsExtension.Tests/DiagnosticTests.cs` | 旧 `AFE0601` の 3 テストを削除し、次の 6 テストを追加 |

| テスト | 期待値 |
| --- | --- |
| `Afe0601NotUsingAzureFunctionsSdkEmitsDiagnostic` | `["AFE0601"]` |
| `Afe0601UsingAzureFunctionsSdkEmitsNoDiagnostic` | 空 |
| `Afe0602MetadataSourceGenDisabledEmitsDiagnostic` | `["AFE0602"]` |
| `Afe0602MetadataProviderAutoRegisterDisabledEmitsDiagnostic` | `["AFE0602"]` |
| `Afe0601IsReportedInsteadOfAfe0602` | `["AFE0601"]` |
| `BuildConfigurationIsNotCheckedWithoutAzureFunctionClass` | 空 |

### 9.7 フェーズ 5: Example.Tests の結合テスト

| ファイル | 種別 | 内容 |
| --- | --- | --- |
| `AzureFunctionsExtension.Example.Tests/FakeFunctionContext.cs` | 変更 | EntryPoint と入力値を受け取るコンストラクターを追加し、`FunctionDefinition` と `Features` (`IFunctionInputBindingFeature`) を実装。既存のコンストラクターの挙動は変えない |
| `AzureFunctionsExtension.Example.Tests/FunctionCatalogTests.cs` | 新規 | 下表 |

テストクラスのコンストラクターで `RuntimeHelpers.RunModuleConstructor(typeof(HttpFunction).Module.ModuleHandle)` を呼び、
Example のモジュール初期化子 (カタログ登録) を確実に実行させる。サービスは既存の `HandlerTestHost.CreateServices()` を使う。

| テスト | 確認内容 |
| --- | --- |
| `MetadataContainsAllFunctions` | 10 関数の EntryPoint、`ScriptFile`、`Language`、`FunctionId` の一意性 |
| `BindingsArePlainJsonWithExpectedTrigger` | `RawBindings` が JSON として解析でき、トリガー種別が期待通り |
| `EntryPointsResolveToPublicStaticMethods` | Worker と同じ方法 (最後の `.` で分割し `Assembly.GetType` + `GetMethod`) で `[Function]` 付きのメソッドが見つかる |
| `ExecutorDispatchesQueueFunction` | `ProcessMessage_Handler` が呼ばれる |
| `ExecutorDispatchesTimerFunction` | `new TimerInfo()` を入力に `Timer_Handler` が呼ばれる |
| `ExecutorThrowsForUnknownEntryPoint` | 内側の Executor が無いため `InvalidOperationException` |

- HTTP のディスパッチは `GetInvocationResult()` が internal な `IFunctionBindingsFeature` を必要とするため単体テストしない (生成テストと E2E で確認)。
- `await` を使うと Example.Tests では `CA2007` が出る (GlobalSuppressions で抑止していない) ため、既存テストと同じく `Task` を返す形にする。

### 9.8 フェーズ 6: 起動時検証の生成と `AFE0205` の拡張

| ファイル | 内容 |
| --- | --- |
| `AzureFunctionsExtension/Hosting/ExtensionStartupValidator.cs` (新規) | 付録 A |
| `AzureFunctionsExtension/Hosting/GeneratedFunctionCatalog.cs` | `ConfigureStartup(IHostBuilder)` を追加 (付録 A) |
| `AzureFunctionsExtension.Generator/FunctionCatalogSourceBuilder.cs` | `FunctionCatalogAutoStartup` の出力を追加 (付録 A) |
| `AzureFunctionsExtension.Generator/FunctionGenerator.cs` | 手書き `[Function]` の関数名を収集するプロバイダーを追加し、`ReportDuplicateFunctionNames` で照合 (付録 A) |

追加テスト:

| テスト | 確認内容 |
| --- | --- |
| `ValidatorSucceedsWhenExtensionExecutorIsUsed` | 正しい登録では成功 |
| `ValidatorThrowsWhenNotRegistered` | 未登録で `AddAzureFunctionExtension()` を含むメッセージの例外 |
| `ValidatorThrowsWhenExecutorIsReplacedAfterRegistration` | 後から Executor が登録されると `after ConfigureFunctionsWebApplication()` を含むメッセージの例外 |
| `ConfigureStartupRegistersValidator` | `ConfigureStartup` で `IHostedService` として登録される |
| `Afe0205HandlerNameDuplicatedWithWorkerFunctionEmitsDiagnostic` | 手書き `[Function("hello")]` と生成の `Hello` で `["AFE0205"]` |
| `Afe0205HandlerNameNotDuplicatedWithWorkerFunctionEmitsNoDiagnostic` | 名前が異なれば空 |

### 9.9 フェーズ 7: ドキュメント

| ファイル | 内容 |
| --- | --- |
| `README.md` | Getting started を新SDKの csproj、`global.json`、`FunctionsApplication.CreateBuilder` 形式の Program.cs に更新。要件 (`Azure.Functions.Sdk` 1.0.1 以降、`global.json` での固定、`AddAzureFunctionExtension()` は必須で `ConfigureFunctionsWebApplication()` の後) を追記 |
| `Diagnostics.md` | Build configuration の表を更新し、Function の表の `AFE0205` の説明に手書き `[Function]` との重複を追記 |
| `NewSdkInvestigation.md` | 実装完了後、残課題を更新 |

`Diagnostics.md` の更新後の行:

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0205 | ❌ Error | Function name is not unique in the app (overloaded, differs only in case, used in another class, or used by a hand-written `[Function]`) | Rename the function so that each function name is unique |
| AFE0601 | ❌ Error | The project does not use `Azure.Functions.Sdk` | Set the project SDK to `Azure.Functions.Sdk` |
| AFE0602 | ❌ Error | The Worker SDK metadata source generation (`FunctionsEnableMetadataSourceGen` or `FunctionsAutoRegisterGeneratedMetadataProvider`) is disabled, so the generated functions are not indexed | Remove the settings that disable them |

### 9.10 フェーズ 8: 総合検証

| 項目 | 状態 |
| --- | --- |
| `dotnet build AzureFunctionsExtension.slnx` (Debug / Release) が警告 0 | 確認済み (Functions プロジェクトを同じ構成で先に復元した場合。3.6) |
| テストが全て成功 | 確認済み (114 件 + 21 件) |
| `dotnet pack` の内容 | 確認済み (7.5 #9) |
| パッケージ参照の新SDKプロジェクトで警告 0、カタログ生成 | 確認済み (7.5 #1) |
| 異常系の診断 (`AFE0601` / `AFE0602` / `AFE0205`) | 確認済み (7.5 #6〜#8) |
| 実ホストの E2E (HTTP / Queue / Timer、手書き関数との混在、`HostBuilder` 方式) | 確認済み (7.4、7.5 #2〜#5) |
| 登録順序の誤り・呼び忘れの起動時検出 | 確認済み (5.5) |
| Windows / Visual Studio でのビルド・デバッグ、Core Tools (`func start` / `dotnet run`) | 未確認。実装後に確認する |
| Azure へのデプロイ | 未確認。実装後に確認する |

テストの実行: テストプロジェクトは Microsoft.Testing.Platform の実行ファイルのため、
`dotnet run --project AzureFunctionsExtension.Tests` などで実行できる
(.NET 10 SDK の `dotnet test` で使うには `global.json` の `test.runner` 設定が必要。9.1 #3)。

---

## 10. 参考

- NuGet: [Azure.Functions.Sdk](https://www.nuget.org/packages/Azure.Functions.Sdk) (パッケージ README / release notes を参照)
- NuGet: [Microsoft.Azure.Functions.Worker](https://www.nuget.org/packages/Microsoft.Azure.Functions.Worker)
- GitHub: [Azure/azure-functions-dotnet-worker](https://github.com/Azure/azure-functions-dotnet-worker) (SDK ルール: `docs/sdk-rules/`)
- Microsoft Learn: [Guide for running C# Azure Functions in an isolated worker process](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- MCR: `mcr.microsoft.com/azure-functions/dotnet-isolated` / `mcr.microsoft.com/azure-storage/azurite`

---

## 付録 A: 検証済みコード

スクラッチ環境で実装し、9 章の検証を通過したコード。行末・インデントはリポジトリの規約 (CRLF、`.editorconfig`) に合わせて作成すること。

### A.1 `AzureFunctionsExtension/Hosting/IGeneratedFunctionCatalog.cs`

```csharp
namespace AzureFunctionsExtension.Hosting;

using System.ComponentModel;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedFunctionCatalog
{
    void AddMetadata(IList<IFunctionMetadata> metadata);

    ValueTask<bool> TryExecuteAsync(FunctionContext context);
}
```

### A.2 `AzureFunctionsExtension/Hosting/GeneratedFunctionCatalog.cs`

```csharp
namespace AzureFunctionsExtension.Hosting;

using System.ComponentModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedFunctionCatalog
{
    private static IGeneratedFunctionCatalog[] catalogs = [];

    internal static IReadOnlyList<IGeneratedFunctionCatalog> Catalogs => Volatile.Read(ref catalogs);

    // Called from the IAutoConfigureStartup implementation generated with the catalog
    public static void ConfigureStartup(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureServices(static services => services.AddSingleton<IHostedService>(static sp => new ExtensionStartupValidator(sp)));
    }

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

### A.3 `AzureFunctionsExtension/Hosting/ExtensionFunctionExecutorState.cs`

```csharp
namespace AzureFunctionsExtension.Hosting;

internal sealed class ExtensionFunctionExecutorState
{
    public bool Attached { get; set; }
}
```

### A.4 `AzureFunctionsExtension/Hosting/ExtensionFunctionMetadataTransformer.cs`

```csharp
namespace AzureFunctionsExtension.Hosting;

using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;

internal sealed class ExtensionFunctionMetadataTransformer : IFunctionMetadataTransformer
{
    private readonly IReadOnlyList<IGeneratedFunctionCatalog>? catalogs;

    public ExtensionFunctionMetadataTransformer(IReadOnlyList<IGeneratedFunctionCatalog>? catalogs = null)
    {
        this.catalogs = catalogs;
    }

    public string Name => "AzureFunctionsExtension";

    public void Transform(IList<IFunctionMetadata> original)
    {
        // Skip functions already provided by another source (e.g. functions.metadata) to avoid duplicated functions and routes
        var existing = new HashSet<string>(original.Select(static x => x.EntryPoint).OfType<string>(), StringComparer.Ordinal);
        var added = new List<IFunctionMetadata>();
        foreach (var catalog in catalogs ?? GeneratedFunctionCatalog.Catalogs)
        {
            catalog.AddMetadata(added);
        }

        foreach (var metadata in added)
        {
            if ((metadata.EntryPoint is null) || existing.Add(metadata.EntryPoint))
            {
                original.Add(metadata);
            }
        }
    }
}
```

### A.5 `AzureFunctionsExtension/Hosting/ExtensionFunctionExecutor.cs`

```csharp
namespace AzureFunctionsExtension.Hosting;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Invocation;

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

### A.6 `AzureFunctionsExtension/Hosting/ExtensionStartupValidator.cs`

```csharp
namespace AzureFunctionsExtension.Hosting;

using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Fails the worker startup when the generated functions cannot be indexed or executed
internal sealed class ExtensionStartupValidator : IHostedService
{
    private readonly IServiceProvider serviceProvider;

    public ExtensionStartupValidator(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var state = serviceProvider.GetService<ExtensionFunctionExecutorState>() ??
            throw new InvalidOperationException("AzureFunctionsExtension is not registered. Call AddAzureFunctionExtension().");

        // Build the executor chain and verify that the extension executor is a part of it
        _ = serviceProvider.GetRequiredService<IFunctionExecutor>();
        if (!state.Attached)
        {
            throw new InvalidOperationException("The function executor of AzureFunctionsExtension is not used. Call AddAzureFunctionExtension() after ConfigureFunctionsWebApplication().");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### A.7 `AzureFunctionsExtension/ServiceCollectionExtensions.cs` (追加部分)

```csharp
// using に追加
using AzureFunctionsExtension.Hosting;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Invocation;

// AddAzureFunctionExtension(IServiceCollection, Action<JsonOptions>) の末尾
        services.TryAddSingleton<IRequestValidator, DataAnnotationsRequestValidator>();
        AddFunctionCatalog(services);
        return services;

// 追加するメソッドは 5.4 の通り (AddFunctionCatalog / ResolveExecutor)
```

### A.8 `AzureFunctionsExtension.Generator/Models/HandlerModelExtensions.cs`

```csharp
namespace AzureFunctionsExtension.Generator.Models;

// スタブ ([Function] メソッド) とメタデータで同じ値を使うため、既定値の解決をここに集約する
// Centralizes default value resolution so that the stub ([Function] method) and the metadata use the same values
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

### A.9 `AzureFunctionsExtension.Generator/FunctionModelBuilder.cs` (追加部分)

```csharp
// new FunctionModel(ns, symbol.Name, GetMetadataName(symbol), ...) として設定する

// ランタイムが EntryPoint の解決に使うメタデータ名 (エスケープ無し) を返す。ToDisplayString() はキーワードに @ を付けるため使わない
// Returns the metadata name (without escaping) used by the runtime to resolve the EntryPoint. ToDisplayString() is not used because it adds @ to keywords
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

### A.10 `AzureFunctionsExtension.Generator/JsonText.cs`

```csharp
namespace AzureFunctionsExtension.Generator;

using System.Globalization;
using System.Text;

// バインディング定義 (JSON) を組み立てるための最小限の書き込みヘルパー
// Minimal writer helpers to build binding definitions (JSON)
internal static class JsonText
{
    public static StringBuilder AppendJsonString(this StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"');
    }

    public static StringBuilder AppendJsonProperty(this StringBuilder builder, string name, string value)
    {
        if (builder[builder.Length - 1] != '{')
        {
            builder.Append(',');
        }

        return builder.AppendJsonString(name).Append(':').AppendJsonString(value);
    }

    public static StringBuilder AppendJsonArrayProperty(this StringBuilder builder, string name, string value)
    {
        if (builder[builder.Length - 1] != '{')
        {
            builder.Append(',');
        }

        return builder.AppendJsonString(name).Append(":[").AppendJsonString(value).Append(']');
    }
}
```

### A.11 `AzureFunctionsExtension.Generator/BindingJsonBuilder.cs`

```csharp
namespace AzureFunctionsExtension.Generator;

using System.Text;

using AzureFunctionsExtension.Generator.Models;

// 関数メタデータの RawBindings (JSON) を組み立てる。形式は Worker SDK のジェネレーターの出力に合わせる
// Builds the RawBindings (JSON) of the function metadata. The format follows the output of the Worker SDK generator
internal static class BindingJsonBuilder
{
    // FunctionSourceBuilder が生成するスタブのパラメーター名と一致させる (入力バインドは名前で対応付けられる)
    // Must match the parameter names of the stubs generated by FunctionSourceBuilder (input bindings are matched by name)
    public const string HttpRequestParameterName = "req";
    public const string TimerInfoParameterName = "timerInfo";
    public const string QueueMessageParameterName = "message";

    private const string HttpOutputBinding = "{\"name\":\"$return\",\"type\":\"http\",\"direction\":\"Out\"}";

    public static string[] Build(HandlerModel handler) =>
        handler.Type switch
        {
            HandlerType.Http => [BuildHttpTrigger(handler), HttpOutputBinding],
            HandlerType.Timer => [BuildTimerTrigger(handler)],
            _ => [BuildQueueTrigger(handler)]
        };

    private static string BuildHttpTrigger(HandlerModel handler) =>
        new StringBuilder()
            .Append('{')
            .AppendJsonProperty("name", HttpRequestParameterName)
            .AppendJsonProperty("type", "httpTrigger")
            .AppendJsonProperty("direction", "In")
            .AppendJsonProperty("authLevel", handler.GetAuthorizationLevel())
            .AppendJsonArrayProperty("methods", handler.GetHttpMethod())
            .AppendJsonProperty("route", handler.GetRoute())
            .Append('}')
            .ToString();

    private static string BuildTimerTrigger(HandlerModel handler) =>
        new StringBuilder()
            .Append('{')
            .AppendJsonProperty("name", TimerInfoParameterName)
            .AppendJsonProperty("type", "timerTrigger")
            .AppendJsonProperty("direction", "In")
            .AppendJsonProperty("schedule", handler.GetTimerSchedule())
            .Append('}')
            .ToString();

    private static string BuildQueueTrigger(HandlerModel handler)
    {
        var builder = new StringBuilder()
            .Append('{')
            .AppendJsonProperty("name", QueueMessageParameterName)
            .AppendJsonProperty("type", "queueTrigger")
            .AppendJsonProperty("direction", "In")
            .AppendJsonProperty("queueName", handler.GetQueueName());
        if (handler.QueueConnection is not null)
        {
            builder.AppendJsonProperty("connection", handler.QueueConnection);
        }

        return builder
            .AppendJsonProperty("dataType", "String")
            .Append('}')
            .ToString();
    }
}
```

### A.12 `AzureFunctionsExtension.Generator/FunctionCatalogSourceBuilder.cs`

```csharp
namespace AzureFunctionsExtension.Generator;

using AzureFunctionsExtension.Generator.Models;

using SourceGenerateHelper;

// 新SDK (ワーカーインデックス) 向けに、生成した関数のメタデータと実行ディスパッチを持つカタログを生成する
// Generates the catalog holding the metadata and the execution dispatch of the generated functions for the new SDK (worker indexing)
internal static class FunctionCatalogSourceBuilder
{
    // ReSharper disable InconsistentNaming
    public const string HintName = "AzureFunctionsExtension.Generated.FunctionCatalog.g.cs";
    private const string CatalogNamespace = "AzureFunctionsExtension.Generated";
    private const string CatalogInterfaceType = "global::AzureFunctionsExtension.Hosting.IGeneratedFunctionCatalog";
    private const string CatalogRegistryType = "global::AzureFunctionsExtension.Hosting.GeneratedFunctionCatalog";
    private const string ModuleInitializerAttributeType = "global::System.Runtime.CompilerServices.ModuleInitializer";
    private const string FunctionMetadataInterfaceType = "global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata";
    private const string DefaultFunctionMetadataType = "global::Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.DefaultFunctionMetadata";
    private const string ListType = "global::System.Collections.Generic.List";
    private const string IListType = "global::System.Collections.Generic.IList";
    private const string FunctionContextType = "global::Microsoft.Azure.Functions.Worker.FunctionContext";
    private const string InputBindingFeatureType = "global::Microsoft.Azure.Functions.Worker.Context.Features.IFunctionInputBindingFeature";
    private const string GetInvocationResultMethod = "global::Microsoft.Azure.Functions.Worker.FunctionContextBindingFeatureExtensions.GetInvocationResult";
    private const string HttpRequestType = "global::Microsoft.AspNetCore.Http.HttpRequest";
    private const string TimerInfoType = "global::Microsoft.Azure.Functions.Worker.TimerInfo";
    private const string ValueTaskBoolType = "global::System.Threading.Tasks.ValueTask<bool>";
    private const string InvalidOperationExceptionType = "global::System.InvalidOperationException";
    private const string AutoConfigureStartupType = "global::Microsoft.Azure.Functions.Worker.IAutoConfigureStartup";
    private const string HostBuilderType = "global::Microsoft.Extensions.Hosting.IHostBuilder";
    // ReSharper restore InconsistentNaming

    public static void Build(SourceBuilder builder, string assemblyName, EquatableArray<FunctionModel> models)
    {
        var functions = models
            .SelectMany(static model => model.Handlers.Select(handler => (Model: model, Handler: handler)))
            .ToList();

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.Disable("CS0612, CS0618");
        builder.NewLine();
        builder.Namespace(CatalogNamespace);
        builder.NewLine();

        builder.AppendLine($"internal sealed class FunctionCatalog : {CatalogInterfaceType}");
        builder.BeginScope();

        builder.AppendLine($"private const string ScriptFile = {CSharpLiteral.Format(assemblyName + ".dll")};");
        builder.NewLine();

        // モジュール初期化子でレジストリに登録する / Register to the registry by the module initializer
        builder.AppendLine($"[{ModuleInitializerAttributeType}]");
        builder.AppendLine("internal static void Initialize()");
        builder.BeginScope();
        builder.AppendLine($"{CatalogRegistryType}.Register(new FunctionCatalog());");
        builder.EndScope();
        builder.NewLine();

        BuildAddMetadata(builder, functions);
        builder.NewLine();
        BuildTryExecute(builder, functions);

        for (var i = 0; i < functions.Count; i++)
        {
            builder.NewLine();
            BuildExecute(builder, i, functions[i].Model, functions[i].Handler);
        }

        builder.NewLine();
        BuildBind(builder);

        builder.EndScope();
        builder.NewLine();

        // ワーカーが起動時に呼び出す。登録漏れや登録順序の誤りを起動時に検出する検証を追加する
        // Invoked by the worker at startup. Adds the validation that detects a missing or misordered registration
        builder.AppendLine($"internal sealed class FunctionCatalogAutoStartup : {AutoConfigureStartupType}");
        builder.BeginScope();
        builder.AppendLine($"public void Configure({HostBuilderType} hostBuilder)");
        builder.BeginScope();
        builder.AppendLine($"{CatalogRegistryType}.ConfigureStartup(hostBuilder);");
        builder.EndScope();
        builder.EndScope();
    }

    private static string GetEntryPoint(FunctionModel model, HandlerModel handler) =>
        model.MetadataName + "." + handler.GetEntryPointMethodName();

    private static void BuildAddMetadata(SourceBuilder builder, List<(FunctionModel Model, HandlerModel Handler)> functions)
    {
        builder.AppendLine($"public void AddMetadata({IListType}<{FunctionMetadataInterfaceType}> metadata)");
        builder.BeginScope();

        for (var i = 0; i < functions.Count; i++)
        {
            var (model, handler) = functions[i];
            var bindings = BindingJsonBuilder.Build(handler);

            if (i > 0)
            {
                builder.NewLine();
            }

            builder.AppendLine($"var bindings{i} = new {ListType}<string>({bindings.Length});");
            foreach (var binding in bindings)
            {
                builder.AppendLine($"bindings{i}.Add({CSharpLiteral.Format(binding)});");
            }

            builder.AppendLine($"metadata.Add(new {DefaultFunctionMetadataType}");
            builder.BeginScope();
            builder.AppendLine("Language = \"dotnet-isolated\",");
            builder.AppendLine($"Name = {CSharpLiteral.Format(handler.MethodName)},");
            builder.AppendLine($"EntryPoint = {CSharpLiteral.Format(GetEntryPoint(model, handler))},");
            builder.AppendLine("ScriptFile = ScriptFile,");
            builder.AppendLine($"RawBindings = bindings{i},");
            builder.IndentLevel--;
            builder.AppendLine("});");
        }

        builder.EndScope();
    }

    private static void BuildTryExecute(SourceBuilder builder, List<(FunctionModel Model, HandlerModel Handler)> functions)
    {
        builder.AppendLine($"public {ValueTaskBoolType} TryExecuteAsync({FunctionContextType} context)");
        builder.BeginScope();
        builder.AppendLine("switch (context.FunctionDefinition.EntryPoint)");
        builder.BeginScope();

        for (var i = 0; i < functions.Count; i++)
        {
            builder.AppendLine($"case {CSharpLiteral.Format(GetEntryPoint(functions[i].Model, functions[i].Handler))}:");
            builder.IndentLevel++;
            builder.AppendLine($"return __Execute{i}__(context);");
            builder.IndentLevel--;
        }

        builder.AppendLine("default:");
        builder.IndentLevel++;
        builder.AppendLine($"return new {ValueTaskBoolType}(false);");
        builder.IndentLevel--;

        builder.EndScope();
        builder.EndScope();
    }

    private static void BuildExecute(SourceBuilder builder, int index, FunctionModel model, HandlerModel handler)
    {
        var stub = $"{model.FunctionType.FullName}.{CSharpIdentifier.Escape(handler.GetEntryPointMethodName())}";

        builder.AppendLine($"private static async {ValueTaskBoolType} __Execute{index}__({FunctionContextType} context)");
        builder.BeginScope();
        builder.AppendLine("var values = await __BindAsync__(context);");
        switch (handler.Type)
        {
            case HandlerType.Http:
                builder.AppendLine($"{GetInvocationResultMethod}(context).Value = await {stub}(({HttpRequestType})values[0]!, context);");
                break;
            case HandlerType.Timer:
                builder.AppendLine($"await {stub}(({TimerInfoType})values[0]!, context);");
                break;
            case HandlerType.Queue:
                builder.AppendLine($"await {stub}((string)values[0]!, context);");
                break;
        }

        builder.AppendLine("return true;");
        builder.EndScope();
    }

    private static void BuildBind(SourceBuilder builder)
    {
        builder.AppendLine($"private static async global::System.Threading.Tasks.ValueTask<object?[]> __BindAsync__({FunctionContextType} context)");
        builder.BeginScope();
        builder.AppendLine($"var feature = context.Features.Get<{InputBindingFeatureType}>() ??");
        builder.AppendLine($"    throw new {InvalidOperationExceptionType}(\"IFunctionInputBindingFeature is not available.\");");
        builder.AppendLine("var result = await feature.BindFunctionInputAsync(context);");
        builder.AppendLine("return result.Values;");
        builder.EndScope();
    }
}
```

### A.13 `AzureFunctionsExtension.Generator/FunctionGenerator.cs` (変更部分)

```csharp
private const string WorkerFunctionAttributeFullName = "Microsoft.Azure.Functions.Worker.FunctionAttribute";

// Initialize: 既存の models の出力の後に追加
// スタブを生成するクラスと同じ集合からカタログを 1 ファイル生成する
// Generate a single catalog file from the same set of classes as the stubs
var assemblyNameProvider = context.CompilationProvider
    .Select(static (compilation, _) => compilation.AssemblyName ?? string.Empty);
var catalogProvider = collected
    .Select(static (results, _) => new EquatableArray<FunctionModel>(SelectModels(results).ToArray()))
    .Combine(assemblyNameProvider);
context.RegisterImplementationSourceOutput(catalogProvider, static (ctx, input) => ExecuteCatalog(ctx, input.Left, input.Right));

// Initialize: 既存の functionNameProvider の登録を置き換え
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

// Initialize: 旧 workerSourceGenProvider を置き換え
var buildConfigProvider = context.AnalyzerConfigOptionsProvider
    .Select(static (provider, _) => new BuildConfigModel(
        IsTrue(provider.GlobalOptions, "UsingAzureFunctionsSdk"),
        IsTrue(provider.GlobalOptions, "FunctionsEnableMetadataSourceGen") &&
        IsTrue(provider.GlobalOptions, "FunctionsAutoRegisterGeneratedMetadataProvider")));
context.RegisterSourceOutput(buildConfigProvider.Combine(treeProvider), static (ctx, input) => ReportBuildConfiguration(ctx, input.Left, input.Right));

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

// ReportDuplicateFunctionNames: 引数に workerNames を追加し、判定を次のように変更
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

// 削除: IsWorkerSourceGenEnabled、ReportWorkerSourceGen
```

### A.14 `AzureFunctionsExtension.Generator/Diagnostics.cs` (Build configuration)

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

### A.15 MSBuild と Example

`AzureFunctionsExtension.props`:

```xml
<Project>

  <!-- Lets AzureFunctionsExtension.Generator detect Azure.Functions.Sdk (AFE0601) -->
  <ItemGroup>
    <CompilerVisibleProperty Include="UsingAzureFunctionsSdk" />
  </ItemGroup>

</Project>
```

`global.json`:

```json
{
  "msbuild-sdks": {
    "Azure.Functions.Sdk": "1.0.1"
  }
}
```

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

`AzureFunctionsExtension.Example/Program.cs` (`KeepExampleTypesAlive` は現行のまま):

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
```

---

## 付録 B: E2E 検証環境の再現手順

Docker が使える環境で、Core Tools を使わずに実際の Functions Host で動作を確認する手順。

```bash
# 1. ネットワークと Azurite
docker network create fx
docker run -d --name azurite --network fx -p 10000-10002:10000-10002 \
  mcr.microsoft.com/azure-storage/azurite \
  azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0 --skipApiVersionCheck --loose

# 2. 管理 API と Function レベルの HTTP 関数を呼ぶためのキー (固定値) を用意する
mkdir -p secrets
cat > secrets/host.json <<'EOF'
{"masterKey":{"name":"master","value":"e2e-master-key","encrypted":false},"functionKeys":[{"name":"default","value":"e2e-function-key","encrypted":false}],"systemKeys":[],"hostName":null,"instanceId":"e2e","source":"runtime","decryptionKeyId":""}
EOF

# 3. Example を発行してホストで起動する
dotnet restore AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj
dotnet publish AzureFunctionsExtension.Example/AzureFunctionsExtension.Example.csproj -c Release -o out/example
docker run -d --name fx-app --network fx -p 18080:80 \
  -v "$PWD/out/example":/home/site/wwwroot:ro \
  -v "$PWD/secrets":/azure-functions-host/Secrets \
  -e AzureWebJobsStorage='DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;TableEndpoint=http://azurite:10002/devstoreaccount1;' \
  -e FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
  -e AzureWebJobsSecretStorageType=files \
  -e AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
  mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0

# 4. 確認
docker logs fx-app 2>&1 | grep -A11 "Found the following functions"
curl http://localhost:18080/api/ping
curl -X POST http://localhost:18080/admin/functions/Timer -H "x-functions-key: e2e-master-key" -H "Content-Type: application/json" -d '{"input":""}'
```

- Queue 関数は、`Azure.Storage.Queues` の `QueueClient` (`MessageEncoding = QueueMessageEncoding.Base64`) で
  `http://127.0.0.1:10001/devstoreaccount1` の `my-queue` へメッセージを送り、ホストのログで `Executed 'Functions.ProcessMessage' (Succeeded` を確認する。
- `AzureWebJobsSecretStorageType=files` とマウントした `host.json` により、管理 API (`/admin/...`) にはマスターキー、
  `AuthorizationLevel.Function` の関数には関数キーを使える。
- 旧構成との比較は、同じ呼び出しの結果をテキストに出力して `diff` で行った (7.4)。
