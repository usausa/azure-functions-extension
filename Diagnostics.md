# Diagnostics

## Class definition

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0001 | ❌ Error | `[AzureFunction]` class is not `partial` | Declare the class as `partial` |
| AFE0002 | ❌ Error | `[AzureFunction]` class is generic | Remove the type parameters from the class |
| AFE0003 | ❌ Error | `[AzureFunction]` class is a nested type | Move the class to the top level |
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
| AFE0008 | ❌ Error | Function name is not unique because the function is overloaded | Rename the function so that each function name is unique |
| AFE0009 | ❌ Error | HTTP-only binding attribute is used on a non-HTTP function | Remove the HTTP-only binding, or use an HTTP trigger |
| AFE0010 | ❌ Error | Timer/Queue function has multiple trigger payload parameters | Leave a single trigger payload parameter |

## Parameter

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0011 | ❌ Error | Parameter has multiple binding attributes | Leave a single binding attribute on the parameter |
| AFE0012 | ❌ Error | Parameter type is not supported by binding | Use a supported parameter type |

## Route

| ID | Severity | Description | How to fix |
|---|---|---|---|
| AFE0013 | ⚠️ Warning | Route template variable is not bound with `[FromRoute]` | Bind the variable with `[FromRoute]`, or remove it from the route template |
