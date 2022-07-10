namespace AzureFunctionsExtension.Bindings;

using System;
using System.Text.Json;
using System.Threading.Tasks;

using AzureFunctionsExtension.Annotations;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;

internal sealed class BindingProvider : IBindingProvider
{
    private readonly IHttpContextAccessor httpContextAccessor;

    private readonly JsonSerializerOptions options;

    public BindingProvider(IHttpContextAccessor httpContextAccessor, JsonSerializerOptions options)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.options = options;
    }

    public Task<IBinding> TryCreateAsync(BindingProviderContext context)
    {
        var bindingAttribute = context.Parameter.CustomAttributes
            .First(attr => attr.AttributeType.CustomAttributes.Any(tag => tag.AttributeType == typeof(BindingAttribute)));

        if (bindingAttribute.AttributeType == typeof(BindBodyAttribute))
        {
            var type = typeof(BodyBinding<>).MakeGenericType(context.Parameter.ParameterType);
            var binding = (IBinding)Activator.CreateInstance(type, httpContextAccessor, options)!;
            return Task.FromResult(binding);
        }
        if (bindingAttribute.AttributeType == typeof(BindQueryAttribute))
        {
            var propertyName = bindingAttribute
                .NamedArguments
                .FirstOrDefault(arg => arg.MemberName == nameof(BindQueryAttribute.Name))
                .TypedValue
                .Value
                ?.ToString() ?? context.Parameter.Name!;
            if (context.Parameter.ParameterType.IsArray)
            {
                var elementType = context.Parameter.ParameterType.GetElementType()!;
                var underlyingElementType = Nullable.GetUnderlyingType(elementType);
                var type = underlyingElementType is null
                    ? typeof(QueryArrayBinding<,>).MakeGenericType(context.Parameter.ParameterType, elementType)
                    : typeof(QueryNullableArrayBinding<,>).MakeGenericType(context.Parameter.ParameterType, underlyingElementType);
                var binding = (IBinding)Activator.CreateInstance(type, propertyName, httpContextAccessor, context.Parameter)!;
                return Task.FromResult(binding);
            }
            else
            {
                var convertType = Nullable.GetUnderlyingType(context.Parameter.ParameterType) ??
                                  context.Parameter.ParameterType;
                var type = typeof(QueryBinding<,>).MakeGenericType(context.Parameter.ParameterType, convertType);
                var binding = (IBinding)Activator.CreateInstance(type, propertyName, httpContextAccessor, context.Parameter)!;
                return Task.FromResult(binding);
            }
        }

        throw new NotSupportedException($"Unknown attribute type. type=[{bindingAttribute.AttributeType}]");
    }
}
