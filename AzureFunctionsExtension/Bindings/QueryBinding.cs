namespace AzureFunctionsExtension.Bindings;

using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

#pragma warning disable CA1812
internal sealed class QueryBinding<T, TConvert> : IBinding
{
    private readonly IValueProvider valueProvider;

    public bool FromAttribute => true;

    public QueryBinding(string name, IHttpContextAccessor httpContextAccessor, ParameterInfo parameter)
    {
        valueProvider = new ValueProvider(name, httpContextAccessor, parameter);
    }

    public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
    {
        throw new NotSupportedException();
    }

    public Task<IValueProvider> BindAsync(BindingContext context)
    {
        return Task.FromResult(valueProvider);
    }

    public ParameterDescriptor ToParameterDescriptor() => new();

    private sealed class ValueProvider : IValueProvider
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        private readonly string name;

        private readonly object? defaultValue;

        public Type Type => typeof(T);

        public ValueProvider(string name, IHttpContextAccessor httpContextAccessor, ParameterInfo parameter)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.name = name;
            defaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : default(T);
        }

        public Task<object?> GetValueAsync()
        {
            if (httpContextAccessor.HttpContext.Request.Query.TryGetValue(name, out var value))
            {
                return Task.FromResult(ConvertHelper.Converter<TConvert>.TryConverter(value, out var result) ? result : defaultValue);
            }

            return Task.FromResult(defaultValue);
        }

        public string ToInvokeString() => string.Empty;
    }
}
#pragma warning restore CA1812

#pragma warning disable CA1812
internal sealed class QueryArrayBinding<TArray, TElement> : IBinding
{
    private readonly IValueProvider valueProvider;

    public bool FromAttribute => true;

    public QueryArrayBinding(string name, IHttpContextAccessor httpContextAccessor, ParameterInfo parameter)
    {
        valueProvider = new ValueProvider(name, httpContextAccessor, parameter);
    }

    public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
    {
        throw new NotSupportedException();
    }

    public Task<IValueProvider> BindAsync(BindingContext context)
    {
        return Task.FromResult(valueProvider);
    }

    public ParameterDescriptor ToParameterDescriptor() => new();

    private sealed class ValueProvider : IValueProvider
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        private readonly string name;

        private readonly object? defaultValue;

        public Type Type => typeof(TArray);

        public ValueProvider(string name, IHttpContextAccessor httpContextAccessor, ParameterInfo parameter)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.name = name;
            defaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : Array.Empty<TElement>();
        }

        public Task<object?> GetValueAsync()
        {
            if (httpContextAccessor.HttpContext.Request.Query.TryGetValue(name, out var values))
            {
                var results = new TElement[values.Count];
                for (var i = 0; i < results.Length; i++)
                {
                    results[i] = ConvertHelper.Converter<TElement>.TryConverter(values[i], out var result) ? result : default!;
                }

                return Task.FromResult<object?>(results);
            }

            return Task.FromResult(defaultValue);
        }

        public string ToInvokeString() => string.Empty;
    }
}
#pragma warning restore CA1812

#pragma warning disable CA1812
internal sealed class QueryNullableArrayBinding<TArray, TElementUnderlying> : IBinding
    where TElementUnderlying : struct
{
    private readonly IValueProvider valueProvider;

    public bool FromAttribute => true;

    public QueryNullableArrayBinding(string name, IHttpContextAccessor httpContextAccessor, ParameterInfo parameter)
    {
        valueProvider = new ValueProvider(name, httpContextAccessor, parameter);
    }

    public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
    {
        throw new NotSupportedException();
    }

    public Task<IValueProvider> BindAsync(BindingContext context)
    {
        return Task.FromResult(valueProvider);
    }

    public ParameterDescriptor ToParameterDescriptor() => new();

    private sealed class ValueProvider : IValueProvider
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        private readonly string name;

        private readonly object? defaultValue;

        public Type Type => typeof(TArray);

        public ValueProvider(string name, IHttpContextAccessor httpContextAccessor, ParameterInfo parameter)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.name = name;
            defaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : Array.Empty<TElementUnderlying?>();
        }

        public Task<object?> GetValueAsync()
        {
            if (httpContextAccessor.HttpContext.Request.Query.TryGetValue(name, out var values))
            {
                var results = new TElementUnderlying?[values.Count];
                for (var i = 0; i < results.Length; i++)
                {
                    results[i] = ConvertHelper.Converter<TElementUnderlying>.TryConverter(values[i], out var result) ? result : default!;
                }

                return Task.FromResult<object?>(results);
            }

            return Task.FromResult(defaultValue);
        }

        public string ToInvokeString() => string.Empty;
    }
}
#pragma warning restore CA1812
