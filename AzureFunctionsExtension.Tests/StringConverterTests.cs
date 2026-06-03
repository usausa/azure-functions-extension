#pragma warning disable CA1707
namespace AzureFunctionsExtension.Tests;

using AzureFunctionsExtension.Binders;

using Xunit;

public sealed class StringConverterTests
{
    // Boolean
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    public void TryToBoolean_ValidValues_ReturnsTrue(string input, bool expected)
    {
        var result = StringConverter.TryToBoolean(input.AsSpan(), out var value);
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryToBoolean_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToBoolean("yes".AsSpan(), out _));
    }

    // Int32
    [Theory]
    [InlineData("0", 0)]
    [InlineData("42", 42)]
    [InlineData("-1", -1)]
    public void TryToInt32_ValidValues_ReturnsTrue(string input, int expected)
    {
        var result = StringConverter.TryToInt32(input.AsSpan(), out var value);
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryToInt32_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToInt32("abc".AsSpan(), out _));
    }

    // Int64
    [Fact]
    public void TryToInt64_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToInt64("9999999999".AsSpan(), out var v));
        Assert.Equal(9999999999L, v);
    }

    [Fact]
    public void TryToInt64_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToInt64("abc".AsSpan(), out _));
    }

    // Int16
    [Fact]
    public void TryToInt16_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToInt16("100".AsSpan(), out var v));
        Assert.Equal((short)100, v);
    }

    // SByte
    [Fact]
    public void TryToSByte_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToSByte("10".AsSpan(), out var v));
        Assert.Equal((sbyte)10, v);
    }

    // UInt32
    [Fact]
    public void TryToUInt32_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToUInt32("100".AsSpan(), out var v));
        Assert.Equal(100u, v);
    }

    [Fact]
    public void TryToUInt32_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToUInt32("-1".AsSpan(), out _));
    }

    // UInt64
    [Fact]
    public void TryToUInt64_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToUInt64("9999999999".AsSpan(), out var v));
        Assert.Equal(9999999999UL, v);
    }

    // UInt16
    [Fact]
    public void TryToUInt16_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToUInt16("500".AsSpan(), out var v));
        Assert.Equal((ushort)500, v);
    }

    // Byte
    [Fact]
    public void TryToByte_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToByte("255".AsSpan(), out var v));
        Assert.Equal((byte)255, v);
    }

    [Fact]
    public void TryToByte_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToByte("256".AsSpan(), out _));
    }

    // Float
    [Fact]
    public void TryToSingle_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToSingle("1.5".AsSpan(), out var v));
        Assert.Equal(1.5f, v);
    }

    // Double
    [Fact]
    public void TryToDouble_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToDouble("3.14".AsSpan(), out var v));
        Assert.Equal(3.14, v, precision: 10);
    }

    [Fact]
    public void TryToDouble_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToDouble("xyz".AsSpan(), out _));
    }

    // Decimal
    [Fact]
    public void TryToDecimal_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToDecimal("1.23".AsSpan(), out var v));
        Assert.Equal(1.23m, v);
    }

    // Char
    [Fact]
    public void TryToChar_SingleChar_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToChar("A".AsSpan(), out var v));
        Assert.Equal('A', v);
    }

    [Fact]
    public void TryToChar_MultipleChars_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToChar("AB".AsSpan(), out _));
    }

    [Fact]
    public void TryToChar_EmptyString_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToChar(string.Empty.AsSpan(), out _));
    }

    // DateTime
    [Fact]
    public void TryToDateTime_ValidIso_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToDateTime("2024-01-15".AsSpan(), out var v));
        Assert.Equal(2024, v.Year);
        Assert.Equal(1, v.Month);
        Assert.Equal(15, v.Day);
    }

    [Fact]
    public void TryToDateTime_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToDateTime("not-a-date".AsSpan(), out _));
    }

    // DateTimeOffset
    [Fact]
    public void TryToDateTimeOffset_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToDateTimeOffset("2024-01-15T10:00:00+09:00".AsSpan(), out var v));
        Assert.Equal(2024, v.Year);
    }

    // DateOnly
    [Fact]
    public void TryToDateOnly_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToDateOnly("2024-06-01".AsSpan(), out var v));
        Assert.Equal(2024, v.Year);
        Assert.Equal(6, v.Month);
    }

    // TimeOnly
    [Fact]
    public void TryToTimeOnly_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToTimeOnly("14:30:00".AsSpan(), out var v));
        Assert.Equal(14, v.Hour);
        Assert.Equal(30, v.Minute);
    }

    // TimeSpan
    [Fact]
    public void TryToTimeSpan_ValidValue_ReturnsTrue()
    {
        Assert.True(StringConverter.TryToTimeSpan("01:30:00".AsSpan(), out var v));
        Assert.Equal(TimeSpan.FromMinutes(90), v);
    }

    // Guid
    [Fact]
    public void TryToGuid_ValidValue_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        Assert.True(StringConverter.TryToGuid(id.ToString().AsSpan(), out var v));
        Assert.Equal(id, v);
    }

    [Fact]
    public void TryToGuid_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToGuid("not-a-guid".AsSpan(), out _));
    }

    // Enum
    public enum Color
    {
        Red,
        Green,
        Blue
    }

    [Theory]
    [InlineData("Red", Color.Red)]
    [InlineData("green", Color.Green)]
    [InlineData("BLUE", Color.Blue)]
    public void TryToEnum_ValidValues_ReturnsTrue(string input, Color expected)
    {
        var result = StringConverter.TryToEnum<Color>(input.AsSpan(), out var value);
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryToEnum_InvalidValue_ReturnsFalse()
    {
        Assert.False(StringConverter.TryToEnum<Color>("Yellow".AsSpan(), out _));
    }
}
