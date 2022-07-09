namespace AzureFunctionsExtension.Bindings;

using System;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Framework only")]
internal sealed class BodyBinding<T> : IBinding
{
    private readonly IValueProvider valueProvider;

    public bool FromAttribute => true;

    public BodyBinding(IHttpContextAccessor httpContextAccessor, JsonSerializerOptions options)
    {
        valueProvider = new ValueProvider(httpContextAccessor, options);
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

        private readonly JsonSerializerOptions options;

        public Type Type => typeof(T);

        public ValueProvider(IHttpContextAccessor httpContextAccessor, JsonSerializerOptions options)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.options = options;
        }

        public async Task<object?> GetValueAsync()
        {
#pragma warning disable CA1031
            try
            {
                return await JsonSerializer.DeserializeAsync<T>(httpContextAccessor.HttpContext.Request.Body, options).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return null;
            }
#pragma warning restore CA1031
        }

        public string ToInvokeString() => string.Empty;
    }
}
