namespace AzureFunctionsExtension.Bindings;

using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Framework only")]
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

    private class ValueProvider : IValueProvider
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

[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Framework only")]
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

    private class ValueProvider : IValueProvider
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

[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Framework only")]
internal sealed class QueryNullableArrayBinding<TArray, TElementUnderlying> : IBinding
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

    private class ValueProvider : IValueProvider
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

internal static class ConvertHelper
{
    public delegate bool TryConverter<T>(string value, out T result);

    public static class Converter<T>
    {
        public static readonly TryConverter<T> TryConverter = ResolveConverter();

        private static TryConverter<T> ResolveConverter()
        {
            var type = typeof(T);
            if (type == typeof(bool))
            {
                return (TryConverter<T>)(object)(TryConverter<bool>)Boolean.TryParse;
            }
            if (type == typeof(char))
            {
                return (TryConverter<T>)(object)(TryConverter<char>)Char.TryParse;
            }
            if (type == typeof(sbyte))
            {
                return (TryConverter<T>)(object)(TryConverter<sbyte>)SByte.TryParse;
            }
            if (type == typeof(byte))
            {
                return (TryConverter<T>)(object)(TryConverter<byte>)Byte.TryParse;
            }
            if (type == typeof(short))
            {
                return (TryConverter<T>)(object)(TryConverter<short>)Int16.TryParse;
            }
            if (type == typeof(ushort))
            {
                return (TryConverter<T>)(object)(TryConverter<ushort>)UInt16.TryParse;
            }
            if (type == typeof(int))
            {
                return (TryConverter<T>)(object)(TryConverter<int>)Int32.TryParse;
            }
            if (type == typeof(uint))
            {
                return (TryConverter<T>)(object)(TryConverter<uint>)UInt32.TryParse;
            }
            if (type == typeof(long))
            {
                return (TryConverter<T>)(object)(TryConverter<long>)Int64.TryParse;
            }
            if (type == typeof(ulong))
            {
                return (TryConverter<T>)(object)(TryConverter<ulong>)UInt64.TryParse;
            }
            if (type == typeof(float))
            {
                return (TryConverter<T>)(object)(TryConverter<float>)Single.TryParse;
            }
            if (type == typeof(double))
            {
                return (TryConverter<T>)(object)(TryConverter<double>)Double.TryParse;
            }
            if (type == typeof(decimal))
            {
                return (TryConverter<T>)(object)(TryConverter<decimal>)Decimal.TryParse;
            }
            if (type == typeof(DateTime))
            {
                return (TryConverter<T>)(object)(TryConverter<DateTime>)DateTime.TryParse;
            }

            var converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(typeof(string)))
            {
                return TypeConverterConverter<T>.TryConvert;
            }

            return AlwaysFailed;
        }

        private static bool AlwaysFailed(string value, out T result)
        {
            result = default!;
            return false;
        }
    }

    private static class TypeConverterConverter<T>
    {
        private static readonly Type Type = typeof(T);

        private static readonly TypeConverter Converter = TypeDescriptor.GetConverter(typeof(T));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Ignore")]
        public static bool TryConvert(string value, out T result)
        {
            try
            {
                result = (T)Converter.ConvertTo(value, Type)!;
                return true;
            }
            catch (Exception)
            {
                result = default!;
                return false;
            }
        }
    }
}
