# AzureFunctionsExtension v2 アップグレード計画

本ドキュメントは `AzureFunctionsExtension` の次期メジャー版（v2.0）における仕様変更と実装計画をまとめたものである。

---

## 1. 背景と目的

### 1.1 現行版 (v1.0.2) の状態

| 項目 | 内容 |
|:-----|:-----|
| ターゲットフレームワーク | `net8.0` |
| ワーカーモデル | **In-Process** (`Microsoft.Azure.WebJobs.*`) |
| バインディング実装 | `IBindingProvider` + `Activator.CreateInstance` + リフレクション |
| HTTPトリガ | `[FunctionName]` + `[HttpTrigger]` + `HttpRequest` |
| JSON 応答 | カスタム `SystemTextJsonResult` ＋ `IActionResultExecutor` |
| 参照ライブラリ | `Microsoft.Azure.WebJobs` 3.0.41 / `Extensions.Http` 3.2.0 / `StringConvertHelper` 2.2.0 |
| AOT 対応 | 不可（リフレクション、`Activator.CreateInstance`） |

### 1.2 アップグレードの動機

1. **.NET 8 → .NET 10** への更新（LTS）。
2. **In-Process ワーカーモデルは 2026年11月10日でサポート終了**。Azure Functions runtime v4 では Isolated worker model が今後の唯一の選択肢となるため、移行は必須。
3. In-Process 専用の `Microsoft.Azure.WebJobs.*` カスタムバインディング API は Isolated では使えない。仕組みを根本から作り直す必要がある。
4. リフレクションを排除し、**Source Generator** によるコード生成へ移行することで AOT 互換性とコールドスタート性能を確保する。
5. 並行プロジェクトである `__Reference/AmazonLambdaExtension`（AWS Lambda 用、`net10.0` ＋ Source Generator 構成）と設計を揃え、保守性を高める。

### 1.3 ゴール

- ユーザーが書くコードの「宣言的なシンプルさ」は v1 と同等以上に保つ。
- Source Generator で `[Function]` ハンドラ本体を自動生成し、ランタイムでのリフレクションを廃止する。
- DI、フィルタパイプライン、バリデーション、JSON シリアライザを差し替え可能にする。
- AOT (`PublishAot=true`) で警告ゼロのビルドを実現する。

---

## 2. 新版（v2.0）の全体構成

### 2.1 プロジェクト構成

```
AzureFunctionsExtension.sln(x)
├── AzureFunctionsExtension/                  (net10.0)        … ランタイムライブラリ
├── AzureFunctionsExtension.Generator/        (netstandard2.0) … Source Generator
├── AzureFunctionsExtension.Example/          (net10.0)        … サンプル (Isolated worker)
└── AzureFunctionsExtension.Tests/            (net10.0)        … ユニット／生成スナップショットテスト
```

`__Reference/AmazonLambdaExtension.Generator` と同じく `netstandard2.0` ターゲットの IncrementalGenerator として実装する。

### 2.2 NuGet パッケージ

| パッケージ | 内容 |
|:-----------|:-----|
| `AzureFunctionsExtension` | ランタイム ＋ Source Generator (`analyzers/dotnet/cs` に同梱) |

利用側は単一パッケージを参照するだけで Source Generator が自動有効化される（`__Reference` の `PackBuildOutputs` ターゲットと同じ方式）。

---

## 3. 新規仕様

### 3.1 ワーカーモデル

**Azure Functions Isolated worker model** に完全移行する。In-Process はサポートしない。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" />
    <PackageReference Include="AzureFunctionsExtension" Version="2.*" />
  </ItemGroup>
</Project>
```

エントリポイントは `Program.cs`：

```csharp
var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Services.AddAzureFunctionExtension(static c =>
{
    c.Options.Converters.Add(new DateTimeConverter());
});
builder.Build().Run();
```

`Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` を使い、HTTP トリガでは ASP.NET Core の `HttpRequest` / `IActionResult` を扱う（v1 とほぼ同じ感覚を保てる）。

### 3.2 ユーザーコードの書き味（Before / After）

**Before (v1, In-Process):**

```csharp
public class Function
{
    [FunctionName("Query")]
    public IActionResult Query(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "query")] HttpRequest req,
        [BindQuery] int a,
        [BindQuery] int? b,
        [BindQuery] int c = 3)
        => Results.Of(new QueryResponse { Result = a + (b ?? 0) + c });
}
```

**After (v2, Isolated + Source Generator):**

```csharp
[AzureFunction]                                  // ← クラス属性
[ServiceResolver(typeof(ServiceResolver))]       // ← DI 注入（任意）
public partial class Function                    // ← partial 必須
{
    private readonly ILogger<Function> log;
    public Function(ILogger<Function> log) => this.log = log;

    [HttpEndpoint("get", "query", AuthorizationLevel.Anonymous)]
    public IActionResult Query(
        [FromQuery] int a,
        [FromQuery] int? b,
        [FromQuery] int c = 3)
        => Results.Of(new QueryResponse { Result = a + (b ?? 0) + c });

    [HttpEndpoint("post", "body", AuthorizationLevel.Function)]
    public IActionResult Body([FromBody] BodyRequest request)
        => Results.Of(new BodyResponse { Id = request.Id, Name = request.Name });
}
```

ユーザーは `HttpRequest req` を引数に書く必要がなくなる（必要なら `HttpRequest`/`FunctionContext` を引数に追加すると自動でバインドされる）。Source Generator が `[Function]` を付けた本物のハンドラを生成する。

### 3.3 属性体系

`AzureFunctionsExtension.Annotations` 名前空間にすべて配置する。

#### クラス属性

| 属性 | 役割 | 必須 |
|:-----|:-----|:-----|
| `[AzureFunction]` | このクラスがハンドラ集合であることを Source Generator に伝える | ○ |
| `[ServiceResolver(typeof(T))]` | DI コンテナの構築方法（`T.ConfigureServices()` を呼ぶ）。コンストラクタ引数を取る場合は必須 | △ |
| `[Filter<TFilter>(Order = N)]` | パイプラインフィルタ。複数指定可、`Order` 昇順でチェイン | × |

#### メソッド属性（ハンドラ種別）

| 属性 | 役割 |
|:-----|:-----|
| `[HttpEndpoint(method, route, level)]` | HTTP トリガ。`method` は `"get"`/`"post"` 等、`level` は `AuthorizationLevel` |
| `[TimerEndpoint(schedule)]` | Timer トリガ（NCRONTAB 式） |
| `[QueueEndpoint(queueName, connection?)]` | Queue Storage トリガ |
| `[ServiceBusEndpoint(queueOrTopic, ...)]` | Service Bus トリガ |
| `[EventGridEndpoint]` | Event Grid トリガ |
| `[GenericTrigger("TriggerType", json)]` | 上記以外の任意トリガ（プロパティを JSON で渡す逃げ道） |

> v2 の初回リリースで実装するのは `[HttpEndpoint]` / `[TimerEndpoint]` / `[QueueEndpoint]` の 3 種類。残りは段階的に追加する。

#### パラメータ属性

| 属性 | バインド元 | HTTP | Timer/Queue/SB |
|:-----|:-----------|:----:|:--------------:|
| `[FromQuery("name"?)]` | URL クエリ | ○ | × |
| `[FromRoute("name"?)]` | URL パスパラメータ | ○ | × |
| `[FromHeader("name"?)]` | HTTP ヘッダ | ○ | × |
| `[FromBody(SkipValidate = false)]` | リクエストボディ (JSON) | ○ | × |
| `[FromServices]` | DI コンテナから解決 | ○ | ○ |
| `[FromTrigger]` | トリガ本体 (Queue メッセージ等) | × | ○ |

属性なしの特別な型はジェネレータが自動認識する：

| 型 | 自動扱い |
|:---|:---------|
| `HttpRequest` / `HttpRequestData` | リクエストオブジェクト |
| `FunctionContext` | 実行コンテキスト |
| `ILogger<T>` | ロガー（DI から取得） |
| `CancellationToken` | `FunctionContext.CancellationToken` |

### 3.4 戻り値

- `IActionResult` / `Task<IActionResult>` / `ValueTask<IActionResult>` を主としてサポート。
- 値型を直接返した場合は内部で `Results.Of(value)` 相当にラップ（`null` なら 404）。
- 戻り値なし（`void` / `Task` / `ValueTask`）は HTTP 以外（Queue/Timer 等）でのみ許容。

### 3.5 JSON シリアライザ

- `JsonOptions` クラス（v1 と同名・同 API）を維持。
- DI 経由で `JsonSerializerOptions` を取得して使う。
- **AOT 向け：** `IBodySerializer` を導入し、`JsonSerializerContext` ベースの差し替えを可能にする（`__Reference` の `JsonBodySerializer` と同じ意匠）。

```csharp
public interface IBodySerializer
{
    T? Deserialize<T>(Stream body);
    Task SerializeAsync<T>(Stream output, T value, CancellationToken ct);
}
```

既定実装：

| 実装 | AOT | 用途 |
|:-----|:---:|:-----|
| `JsonBodySerializer(JsonSerializerOptions)` | × | 既定。リフレクション使用。`[RequiresDynamicCode]` |
| `JsonBodySerializer(JsonSerializerContext)` | ○ | 利用者が `[JsonSerializable(typeof(T))]` で生成したコンテキストを渡す |

### 3.6 バリデーション

- `IRequestValidator` インタフェースを導入（`__Reference` と同形）。
- 既定実装 `DataAnnotationsRequestValidator` を提供。
- `[FromBody]` パラメータは既定でバリデートされ、失敗時は 400 を返す。`SkipValidate = true` で抑止可能。

### 3.7 フィルタパイプライン

```csharp
public interface IFunctionFilter
{
    ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next);
}

public sealed class FunctionInvocationContext
{
    public object? Request { get; init; }
    public FunctionContext FunctionContext { get; init; } = default!;
    public CancellationToken CancellationToken { get; init; }
    public object? Result { get; set; }
    public IDictionary<string, object?> Items { get; }
}
```

`[Filter<LoggingFilter>(Order = 0)]` のように複数チェイン可能、`Order` 昇順で起動される（`__Reference` 準拠）。

> ※ Azure Functions Isolated には公式の `IFunctionsWorkerMiddleware` が存在するが、本ライブラリのフィルタはハンドラ単位での pre/post 処理と結果置換が目的のため、Source Generator が生成する `_Handler` の内側で別途チェインを構築する。

### 3.8 例外・エラーモデル

- `ApiException(int statusCode, string message)` を導入。フィルタやハンドラから投げると、自動的に対応する HTTP レスポンスに変換される（`__Reference` と同設計）。
- 想定外の例外は `ILogger` でログ出力し 500 を返す。

### 3.9 Diagnostics（Source Generator 警告／エラー）

`__Reference` の ALExxxx 体系を `AFExxxx` として踏襲する。

| ID | 重大度 | 内容 |
|:---|:------:|:-----|
| `AFE0001` | Error | `[AzureFunction]` クラスが `partial` でない |
| `AFE0002` | Warning | ハンドラ属性なしの公開メソッドを検知 |
| `AFE0003` | Error | ハンドラ属性の重複付与 |
| `AFE0004` | Error | パラメータバインド属性の重複付与 |
| `AFE0005` | Error | 非 HTTP ハンドラに `[FromBody]` / `[FromQuery]` 等が付与されている |
| `AFE0006` | Error | バインドで扱えない型（ジェネレータが対応していない） |
| `AFE0007` | Error | コンストラクタ引数があるのに `[ServiceResolver]` が無い |
| `AFE0008` | Error | `ServiceResolver` 型に `public static IServiceCollection ConfigureServices()` が無い |
| `AFE0009` | Error | フィルタ型が `IFunctionFilter` を実装していない |
| `AFE0010` | Warning | `[HttpEndpoint]` の `route` 中のテンプレート変数が `[FromRoute]` で受けられていない |

---

## 4. Source Generator の設計

`__Reference/AmazonLambdaExtension.Generator` をテンプレートに、Azure Functions Isolated 向けに調整する。

### 4.1 入出力モデル

```
[AzureFunction] partial class           ─┐
[ServiceResolver(typeof(T))]             │
[Filter<F>(Order=N)]                     ├─► ModelBuilder ─► FunctionModel
public IActionResult Method([From...]…) ─┘                    ├─ HandlerModel[]
                                                              ├─ FilterDescriptorModel[]
                                                              └─ ServiceResolverModel?

FunctionModel ─► WrapperBuilder ─► 生成ファイル
                                    ├─ {Class}__shared__.g.cs     （static フィールド：DI、フィルタ、シリアライザ）
                                    └─ {Class}__{Method}.g.cs     （[Function("Method")] 付きラッパー）
```

すべて `IIncrementalGenerator` + `ForAttributeWithMetadataName` ベース（`__Reference` の `LambdaGenerator.cs` と同形）。

### 4.2 生成コードの形（HTTP ハンドラ例）

ユーザーが書く：

```csharp
[AzureFunction]
[ServiceResolver(typeof(ServiceResolver))]
public partial class Function
{
    private readonly ILogger<Function> log;
    public Function(ILogger<Function> log) => this.log = log;

    [HttpEndpoint("get", "query", AuthorizationLevel.Anonymous)]
    public IActionResult Query([FromQuery] int a, [FromQuery] int? b, [FromQuery] int c = 3)
        => Results.Of(new QueryResponse { Result = a + (b ?? 0) + c });
}
```

ジェネレータが生成する（概念）：

```csharp
// <auto-generated/>
#nullable enable
namespace MyApp;

partial class Function
{
    // __shared__.g.cs
    private static readonly System.IServiceProvider __provider__ =
        Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
            .BuildServiceProvider(ServiceResolver.ConfigureServices());

    private static readonly Function __target__ =
        new Function(__provider__.GetRequiredService<ILogger<Function>>());

    private static readonly IBodySerializer __bodySerializer__ =
        __provider__.GetRequiredService<IBodySerializer>();

    private static readonly IRequestValidator __requestValidator__ =
        __provider__.GetRequiredService<IRequestValidator>();
}

partial class Function
{
    // Function__Query.g.cs
    [Microsoft.Azure.Functions.Worker.Function("Query")]
    public static Microsoft.AspNetCore.Mvc.IActionResult Query_Handler(
        [Microsoft.Azure.Functions.Worker.HttpTrigger(
            Microsoft.Azure.Functions.Worker.AuthorizationLevel.Anonymous,
            "get", Route = "query")]
        Microsoft.AspNetCore.Http.HttpRequest req,
        Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        try
        {
            // [FromQuery] int a
            int p0 = default;
            if (req.Query.TryGetValue("a", out var p0raw))
            {
                if (!StringConvert.TryToInt32(p0raw, out p0))
                    return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult("Invalid parameter: a");
            }

            // [FromQuery] int? b
            int? p1 = null;
            if (req.Query.TryGetValue("b", out var p1raw))
            {
                if (!StringConvert.TryToInt32(p1raw, out var p1tmp))
                    return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult("Invalid parameter: b");
                p1 = p1tmp;
            }

            // [FromQuery] int c = 3
            int p2 = 3;
            if (req.Query.TryGetValue("c", out var p2raw))
            {
                if (!StringConvert.TryToInt32(p2raw, out p2))
                    return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult("Invalid parameter: c");
            }

            return __target__.Query(p0, p1, p2);
        }
        catch (AzureFunctionsExtension.ApiException ex)
        {
            return new Microsoft.AspNetCore.Mvc.ObjectResult(ex.Message) { StatusCode = ex.StatusCode };
        }
        catch (System.Exception ex)
        {
            context.GetLogger("Query").LogError(ex, "Unhandled exception");
            return new Microsoft.AspNetCore.Mvc.StatusCodeResult(500);
        }
    }
}
```

### 4.3 フィルタが付いた場合

`__Reference` と同様に：

1. `static readonly Pipeline` を 1 度だけ構築（`__filter0__ ─► __filter1__ ─► __MethodName_Inner__`）
2. `_Handler` メソッド内で `FunctionInvocationContext` を組み、`await __Pipeline__(ctx)`
3. `ctx.Result` を取り出して返す

### 4.4 Body 引数のバリデーション

```csharp
var p0 = __bodySerializer__.Deserialize<BodyRequest>(req.Body);
if (p0 is null) return new BadRequestObjectResult("Invalid body");
if (!__requestValidator__.Validate(p0)) return new BadRequestObjectResult("Validation failed");
```

`[FromBody(SkipValidate = true)]` ならバリデーション節は出力しない。

### 4.5 型変換

`__Reference` の `GetConverterMethod` をそのまま流用。`StringConvertHelper`（最新版）を依存に持つ。対応型：bool/各種整数/浮動小数/decimal/char/DateTime/DateTimeOffset/DateOnly/TimeOnly/TimeSpan/Guid/string/enum/配列。

### 4.6 AOT 対応

- 生成コードは `Activator.CreateInstance` を一切使わない。
- `JsonBodySerializer(JsonSerializerOptions)` には `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]` を付与し、AOT 利用者には `JsonSerializerContext` を渡してもらう。
- ライブラリ本体は `<IsAotCompatible>true</IsAotCompatible>` を有効化。

---

## 5. 破壊的変更とマイグレーションガイド

### 5.1 必須の変更点（既存利用者向け）

| v1 | v2 | 備考 |
|:---|:---|:-----|
| In-Process worker | Isolated worker | プロジェクトテンプレートと `Program.cs` を作り直し |
| `[FunctionName("X")]` | `[HttpEndpoint(...)]` etc. ＋ クラスに `[AzureFunction]` | クラスは `partial` 必須 |
| `[BindQuery]` | `[FromQuery]` | 名前を ASP.NET Core 標準に揃える |
| `[BindBody]` | `[FromBody]` | 同上 |
| `[HttpTrigger]` をパラメータに直書き | クラス＋メソッドの属性で表現 | `HttpRequest` を引数に書くのは任意 |
| `Startup : FunctionsStartup` | `Program.cs` の `FunctionsApplication.CreateBuilder` | Isolated に必要 |
| `AddAzureFunctionExtension(...)` | 同名 API を維持 | シグネチャ互換 |
| `Results.Of(...)` / `SystemTextJsonResult` | 同名 API を維持 | 公開面は変更なし |

### 5.2 NuGet 依存の入れ替え

旧：
```
Microsoft.Azure.WebJobs 3.0.41
Microsoft.Azure.WebJobs.Extensions 5.0.0
Microsoft.Azure.WebJobs.Extensions.Http 3.2.0
StringConvertHelper 2.2.0
```

新：
```
Microsoft.Azure.Functions.Worker                        (latest)
Microsoft.Azure.Functions.Worker.Sdk                    (latest)
Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore (latest)
Microsoft.Extensions.DependencyInjection 10.0.0
StringConvertHelper                                     (latest)
```

---

## 6. 実装計画

### Phase 0 — 準備（0.5 日）

- [ ] `Directory.Build.props` の `TargetFramework` / 共通 `<Version>` を 2.0.0 に更新。
- [ ] `.editorconfig` / `Analyzers.ruleset` を `__Reference` と同期。
- [ ] ソリューションに `AzureFunctionsExtension.Generator`, `AzureFunctionsExtension.Tests` を追加。

### Phase 1 — ランタイムライブラリ骨格（1 日）

- [ ] `net10.0` 化、`<IsAotCompatible>true</IsAotCompatible>` 有効化。
- [ ] `Microsoft.Azure.WebJobs.*` 系の削除、Worker 系 NuGet への入れ替え。
- [ ] 旧 `Bindings/`, `BindingStartup.cs`, 旧 `BindQuery/BindBody` 属性を削除。
- [ ] `Annotations/` に新属性 (`AzureFunctionAttribute`, `HttpEndpointAttribute`, `FromQuery/From...`, `ServiceResolverAttribute`, `FilterAttribute<T>`, ...) を追加。
- [ ] `Serialization/IBodySerializer` + `JsonBodySerializer` 実装（`__Reference` 流用）。
- [ ] `Validation/IRequestValidator` + `DataAnnotationsRequestValidator` 実装。
- [ ] `Filters/IFunctionFilter` + `FunctionInvocationContext` + delegate 定義。
- [ ] `ApiException` 追加。
- [ ] `ServiceCollectionExtensions.AddAzureFunctionExtension` を Isolated 向けに書き直し。
- [ ] 既存の `Results` / `SystemTextJsonResult` は ASP.NET Core integration を前提に整理（不要なら削除し `Results.Of` のみ残す）。

### Phase 2 — Source Generator（2 日）

`__Reference/AmazonLambdaExtension.Generator` をテンプレートに、以下を移植・改変：

- [ ] `AzureFunctionsExtension.Generator.csproj`（`netstandard2.0`, `IsRoslynComponent`）。
- [ ] `FunctionGenerator.cs` … `[AzureFunction]` 属性で `ForAttributeWithMetadataName`、incremental 値で `SourceProductionContext` に書き出す。
- [ ] `ModelBuilder.cs` … クラス／メソッド／パラメータの抽出と検証（AFE0001〜AFE0010）。
- [ ] `WrapperBuilder.cs` … HTTP / Timer / Queue ハンドラそれぞれの生成テンプレート。
- [ ] `Models/FunctionModel.cs` … incremental に必要な `record` ＋ `EquatableArray` モデル群。
- [ ] `Diagnostics.cs` … 上記 AFExxxx の `DiagnosticDescriptor`。
- [ ] パッケージング：`Directory.Build.props` の `PackBuildOutputs` ターゲットで `AzureFunctionsExtension.Generator.dll` と `SourceGenerateHelper.dll` を `analyzers/dotnet/cs` に同梱（`__Reference` 流用）。

### Phase 3 — Example プロジェクト書き換え（0.5 日）

- [ ] `AzureFunctionsExtension.Example` を Isolated worker テンプレート（`Program.cs`）に作り替え。
- [ ] `Function.cs` を新属性で書き直し（v2 のショーケース）。
- [ ] `host.json` / `local.settings.json` を Isolated 用に更新（`FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`）。
- [ ] `ServiceResolver.cs` を追加（DI セットアップ）。

### Phase 4 — テスト（1 日）

- [ ] `AzureFunctionsExtension.Tests` 新規作成（xUnit）。
- [ ] **Source Generator スナップショットテスト**：`Verify.SourceGenerators` で代表ケース（Query/Body/Route/フィルタ付き/DI 無し/Timer/Queue）を凍結。
- [ ] **Diagnostics テスト**：AFE0001〜AFE0010 がそれぞれ意図したシナリオで出ることを確認。
- [ ] **ランタイムテスト**：`StringConvert` 経由のスカラ／配列バインドユニットテスト、`DataAnnotationsRequestValidator` テスト、`JsonBodySerializer` の AOT/非 AOT 双方テスト。
- [ ] **E2E**：Example を `func start` で起動してエンドポイントを叩く GitHub Actions ワークフロー（任意・後回し可）。

### Phase 5 — ドキュメント・リリース（0.5 日）

- [ ] `README.md` を v2 仕様に全面改稿（Quick start / 属性一覧 / Migration from v1 / AOT 利用法）。
- [ ] `CHANGELOG.md` を起こす。
- [ ] `Directory.Build.props` の `<Version>` を `2.0.0` に。
- [ ] NuGet パッケージ生成設定（`__Reference` の `PackBuildOutputs` を参考）。
- [ ] GitHub Actions（`.github/workflows`）の TFM/SDK を更新。

> **総工数目安：5 日**（テスト記述まで含む。1 人作業前提）

---

## 7. リスクと対策

| リスク | 影響 | 対策 |
|:-------|:-----|:-----|
| Isolated worker のカスタムバインディング API は In-Process と全く別物 | 設計が根本から変わる | バインディングは Worker 拡張に依存せず、Source Generator が `[Function]` メソッド内で **自力で `HttpRequest` から値を取り出す**設計とする（`__Reference` と同じアプローチ）。これでランタイム拡張は不要。 |
| ASP.NET Core integration が要件（`HttpRequest`/`IActionResult` を使うため） | 利用者の `Program.cs` で `ConfigureFunctionsWebApplication()` を呼ぶ必要 | README とサンプルで明示。Source Generator は不在を検知できないので、必要なら起動時アサーションを追加。 |
| Source Generator のキャッシュが効かないと IDE が重くなる | 開発体験悪化 | `record` + `EquatableArray<T>`（`SourceGenerateHelper`）で incremental 等価性を厳守。`__Reference` のモデルクラスをそのまま使用。 |
| AOT で `JsonSerializer` がリフレクションを使うと警告 | AOT ユーザーの体験悪化 | `JsonBodySerializer(JsonSerializerContext)` 経路を推奨パスとして README で強調。 |
| 旧 v1 ユーザーの破壊的変更が大きい | アップグレード障壁 | v1 系（`net8.0` In-Process）はそのまま残し、v2 を別メジャーとして公開。`MIGRATION.md` に手順を明記。 |
| `Microsoft.Azure.Functions.Worker.*` の最新バージョン番号は時期によって変動 | csproj 固定値が陳腐化 | 本ドキュメントは具体バージョンを書かず `latest` と表現。実装時に `dotnet list package --outdated` で最終確定。 |

---

## 8. 未決事項（要レビュー）

以下は実装着手前に方針確認したい点：

1. **ハンドラ属性の命名**：`[HttpEndpoint]` か `[HttpFunction]` か、それとも v1 の `[HttpTrigger]` を別物として再導入するか。
2. **公開メソッドのデフォルト挙動**：ハンドラ属性なしの公開メソッドを `AFE0002` Warning にするか、単に無視するか（`__Reference` は Warning）。
3. **v1 系のメンテナンス方針**：`main` ブランチを v2 にし、v1 は `v1.x` ブランチで保守するのか、あるいは v1 は完全に終了とするのか。
4. **NuGet パッケージの分割**：`AzureFunctionsExtension.Annotations`（注釈のみ）と `AzureFunctionsExtension`（ランタイム）の 2 分割は不要か（現状の `__Reference` は単一パッケージ）。

---

## 9. 完了の定義 (Definition of Done)

- [ ] `dotnet build -c Release` が警告 0 で通る（`WarningsAsErrors=nullable` 維持）。
- [ ] Example プロジェクトが `func start` で起動し、Query/Body/Timer/Queue 各エンドポイントが期待どおり応答する。
- [ ] Example プロジェクトが `dotnet publish -c Release -r linux-x64 /p:PublishAot=true` を AOT 警告 0 で完了する（`JsonSerializerContext` 経路使用時）。
- [ ] スナップショットテストがすべて緑。
- [ ] `README.md` に Quick start / 属性一覧 / Migration from v1 / AOT 手順 が記載。
- [ ] NuGet パッケージ `AzureFunctionsExtension 2.0.0` が `dotnet pack` で生成され、Generator DLL が `analyzers/dotnet/cs` に同梱されていることを `unzip -l *.nupkg` で確認。
