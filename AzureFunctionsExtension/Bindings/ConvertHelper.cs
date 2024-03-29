namespace AzureFunctionsExtension.Bindings;

using System;
using System.ComponentModel;

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
        private static readonly TypeConverter Converter = TypeDescriptor.GetConverter(typeof(T));

        public static bool TryConvert(string value, out T result)
        {
#pragma warning disable CA1031
            try
            {
                result = (T)Converter.ConvertFrom(value)!;
                return true;
            }
            catch (Exception)
            {
                result = default!;
                return false;
            }
#pragma warning restore CA1031
        }
    }
}
