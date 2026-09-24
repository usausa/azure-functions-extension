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
| AFE0006 | ❌ Error | Filter type does not implement `IFunctionFilter` | Implement `IFunctionFilter` on the filter type |

## Function

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0007 | ❌ Error | Function has multiple endpoint attributes | Leave a single endpoint attribute on the function |
| AFE0008 | ❌ Error | Function name is not unique in the app (overloaded, differs only in case, or used in another class) | Rename the function so that each function name is unique |
| AFE0009 | ❌ Error | HTTP-only binding attribute is used on a non-HTTP function | Remove the HTTP-only binding, or use an HTTP trigger |
| AFE0010 | ❌ Error | `[TimerEndpoint]` / `[QueueEndpoint]` function has multiple trigger payload parameters | Leave a single trigger payload parameter |
| AFE0014 | ❌ Error | Function is a generic method | Remove the type parameters from the function |
| AFE0016 | ❌ Error | `[TimerEndpoint]` trigger payload is not `TimerInfo`, or `[QueueEndpoint]` payload is not `string` (the generated function passes those types) | Receive `TimerInfo` / `string` and convert in the function |
| AFE0017 | ⚠️ Warning | Handler is declared in a base class, so it does not become a function (reported once at the base method) | Declare the handler in the `[AzureFunction]` class |
| AFE0019 | ❌ Error | `[HttpEndpoint]` `AuthorizationLevel` is not a defined value | Specify a defined `AuthorizationLevel` |
| AFE0020 | ❌ Error | Class name differs only in case from another `[AzureFunction]` class, so the generated file names collide; only the first class (in ordinal order) is generated | Rename one of the classes |

## Parameter

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0011 | ❌ Error | Parameter has multiple binding attributes | Leave a single binding attribute on the parameter |
| AFE0012 | ❌ Error | Parameter type is not supported by binding | Use a supported parameter type |
| AFE0018 | ⚠️ Warning | Parameter has an attribute with the name of a binding attribute (`[FromHeader]` and so on) from another library such as ASP.NET Core MVC, which is ignored | Use the attribute of `AzureFunctionsExtension.Annotations` |

## Route

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0013 | ⚠️ Warning | Route template variable is not bound with `[FromRoute]` | Bind the variable with `[FromRoute]`, or remove it from the route template |

## Build configuration

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0015 | ❌ Error | The Worker SDK source generation (`FunctionsEnableWorkerIndexing` or `FunctionsEnableExecutorSourceGen`) is enabled, so the generated functions are not registered | Remove the settings that enable them (the package sets both to `false`) |
