# Diagnostics

## Class definition

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0001 | ❌ Error | `[AzureFunction]` class is not `partial` | Declare the class as `partial` |
| AFE0002 | ❌ Error | `[AzureFunction]` class is generic | Remove the type parameters from the class |
| AFE0003 | ❌ Error | `[AzureFunction]` class is a nested or file-local type | Move the class to the top level |
| AFE0004 | ❌ Error | `[AzureFunction]` is applied to a record | Declare the target as a class |
| AFE0005 | ❌ Error | `[AzureFunction]` class is `abstract` | Make the class non-abstract |

## Filter

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0101 | ❌ Error | Filter type does not implement `IFunctionFilter` | Implement `IFunctionFilter` on the filter type |

## Function

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0201 | ❌ Error | `[HttpEndpoint]` `AuthorizationLevel` is not a defined value | Specify a defined `AuthorizationLevel` |
| AFE0202 | ❌ Error | Function has multiple endpoint attributes | Leave a single endpoint attribute on the function |
| AFE0203 | ❌ Error | Function is a generic method | Remove the type parameters from the function |
| AFE0204 | ❌ Error | `[TimerEndpoint]` / `[QueueEndpoint]` function has multiple trigger payload parameters | Leave a single trigger payload parameter |
| AFE0205 | ❌ Error | Function name is not unique in the app (overloaded, differs only in case, used in another class, or used by a hand-written `[Function]`) | Rename the function so that each function name is unique |
| AFE0206 | ⚠️ Warning | Handler is declared in a base class, so it does not become a function (reported once at the base method) | Declare the handler in the `[AzureFunction]` class |

## Parameter

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0301 | ❌ Error | HTTP-only binding (`[FromQuery]` / `[FromRoute]` / `[FromHeader]` / `[FromCookie]` / `[FromBody]`, or an `HttpRequest` / `ClaimsPrincipal` parameter) is used on a non-HTTP function | Remove the HTTP-only binding, or use an HTTP trigger |
| AFE0302 | ❌ Error | Parameter has multiple binding attributes | Leave a single binding attribute on the parameter |
| AFE0303 | ❌ Error | Parameter type is not supported by binding (an array bound with `[FromQuery]` / `[FromHeader]` / `[FromRoute]` must be one-dimensional; jagged and multidimensional arrays are not supported; `[FromCookie]` reads a single value, so arrays are not supported) | Use a supported parameter type |
| AFE0304 | ❌ Error | `[TimerEndpoint]` trigger payload is not `TimerInfo`, or `[QueueEndpoint]` payload is not `string` (the generated function passes those types) | Receive `TimerInfo` / `string` and convert in the function |
| AFE0305 | ⚠️ Warning | Parameter has an attribute with the name of a binding attribute (`[FromHeader]` and so on) from another library such as ASP.NET Core MVC, which is ignored | Use the attribute of `AzureFunctionsExtension.Annotations` |

## Route

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0401 | ⚠️ Warning | Route template variable is not bound with `[FromRoute]` | Bind the variable with `[FromRoute]`, or remove it from the route template |

## Generation

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0501 | ❌ Error | Class name differs only in case from another `[AzureFunction]` class, so the generated file names collide; only the first class (in ordinal order) is generated | Rename one of the classes |

## Build configuration

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0601 | ❌ Error | The project does not use `Azure.Functions.Sdk` | Set the project SDK to `Azure.Functions.Sdk` |
| AFE0602 | ❌ Error | The Worker SDK metadata source generation (`FunctionsEnableMetadataSourceGen` or `FunctionsAutoRegisterGeneratedMetadataProvider`) is disabled, so the generated functions are not indexed | Remove the settings that disable them |
