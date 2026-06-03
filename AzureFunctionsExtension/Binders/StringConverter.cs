namespace AzureFunctionsExtension.Binders;

#pragma warning disable CA1305, CS3001
public static class StringConverter
{
    public static bool TryToBoolean(ReadOnlySpan<char> value, out bool result)
        => Boolean.TryParse(value, out result);

    public static bool TryToByte(ReadOnlySpan<char> value, out byte result)
        => Byte.TryParse(value, out result);

    public static bool TryToSByte(ReadOnlySpan<char> value, out sbyte result)
        => SByte.TryParse(value, out result);

    public static bool TryToInt16(ReadOnlySpan<char> value, out short result)
        => Int16.TryParse(value, out result);

    public static bool TryToUInt16(ReadOnlySpan<char> value, out ushort result)
        => UInt16.TryParse(value, out result);

    public static bool TryToInt32(ReadOnlySpan<char> value, out int result)
        => Int32.TryParse(value, out result);

    public static bool TryToUInt32(ReadOnlySpan<char> value, out uint result)
        => UInt32.TryParse(value, out result);

    public static bool TryToInt64(ReadOnlySpan<char> value, out long result)
        => Int64.TryParse(value, out result);

    public static bool TryToUInt64(ReadOnlySpan<char> value, out ulong result)
        => UInt64.TryParse(value, out result);

    public static bool TryToSingle(ReadOnlySpan<char> value, out float result)
        => Single.TryParse(value, out result);

    public static bool TryToDouble(ReadOnlySpan<char> value, out double result)
        => Double.TryParse(value, out result);

    public static bool TryToDecimal(ReadOnlySpan<char> value, out decimal result)
        => Decimal.TryParse(value, out result);

    public static bool TryToChar(ReadOnlySpan<char> value, out char result)
    {
        if (value.Length == 1)
        {
            result = value[0];
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryToDateTime(ReadOnlySpan<char> value, out DateTime result)
        => DateTime.TryParse(value, out result);

    public static bool TryToDateTimeOffset(ReadOnlySpan<char> value, out DateTimeOffset result)
        => DateTimeOffset.TryParse(value, out result);

    public static bool TryToDateOnly(ReadOnlySpan<char> value, out DateOnly result)
        => DateOnly.TryParse(value, out result);

    public static bool TryToTimeOnly(ReadOnlySpan<char> value, out TimeOnly result)
        => TimeOnly.TryParse(value, out result);

    public static bool TryToTimeSpan(ReadOnlySpan<char> value, out TimeSpan result)
        => TimeSpan.TryParse(value, out result);

    public static bool TryToGuid(ReadOnlySpan<char> value, out Guid result)
        => Guid.TryParse(value, out result);

    public static bool TryToEnum<T>(ReadOnlySpan<char> value, out T result)
        where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out result);
}
#pragma warning restore CA1305, CS3001
