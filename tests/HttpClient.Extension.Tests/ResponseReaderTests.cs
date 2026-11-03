using System.Globalization;
using System.Text;
using Xunit;

namespace HttpClient.Extension.Tests;

public class ResponseReaderTests
{
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task ProtocolNumbersDoNotDependOnCurrentCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            await AssertValue("255", byte.MaxValue);
            await AssertValue("-128", sbyte.MinValue);
            await AssertValue("-32768", short.MinValue);
            await AssertValue("65535", ushort.MaxValue);
            await AssertValue("-2147483648", int.MinValue);
            await AssertValue("4294967295", uint.MaxValue);
            await AssertValue("-9223372036854775808", long.MinValue);
            await AssertValue("18446744073709551615", ulong.MaxValue);
            await AssertValue("1.5", 1.5f);
            await AssertValue("1.5", 1.5d);
            await AssertValue("1.5", 1.5m);
            await AssertValue("1e-3", 0.001m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("1,234.56")]
    public async Task ProtocolNumbersRejectAmbiguousGrouping(string value)
    {
        using var content = new StringContent(value);
        await Assert.ThrowsAsync<FormatException>(() => content.ReadAsync<decimal>(true));
    }

    [Fact]
    public async Task NullableScalarsAcceptRawAndJsonStringValues()
    {
        var guid = Guid.Parse("eb382030-f571-4ee2-b19a-44466e4c90a0");
        await AssertValue<Guid?>(guid.ToString(), guid);
        await AssertValue<Guid?>($"\"{guid}\"", guid);
        await AssertValue<int?>("42", 42);
        await AssertValue<int?>("\"42\"", 42);
        await AssertValue<decimal?>("1.5", 1.5m);
        await AssertValue<decimal?>("\"1.5\"", 1.5m);
        await AssertValue<bool?>("True", true);
        await AssertValue<bool?>("\"false\"", false);
        await AssertValue<char?>("x", 'x');
        await AssertValue<char?>("\"x\"", 'x');
        var date = new DateTime(2026, 11, 3, 12, 30, 0);
        await AssertValue<DateTime?>("2026-11-03T12:30:00", date);
        await AssertValue<DateTime?>("\"2026-11-03T12:30:00\"", date);
        var offset = new DateTimeOffset(date, TimeSpan.FromHours(8));
        await AssertValue<DateTimeOffset?>("2026-11-03T12:30:00+08:00", offset);
        await AssertValue<TimeSpan?>("01:02:03", new TimeSpan(1, 2, 3));
        await AssertValue<TimeSpan?>("\"01:02:03\"", new TimeSpan(1, 2, 3));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    [InlineData("\"\"")]
    public async Task EmptyNullableScalarsRemainNull(string value)
    {
        await AssertValue<int?>(value, null);
        await AssertValue<decimal?>(value, null);
        await AssertValue<Guid?>(value, null);
        await AssertValue<DateTime?>(value, null);
        await AssertValue<bool?>(value, null);
        await AssertValue<char?>(value, null);
    }

    [Fact]
    public async Task ExistingEmptyAndTextSemanticsArePreserved()
    {
        await AssertValue("", 0);
        await AssertValue(" ", false);
        await AssertValue("", default(char));
        await AssertValue(" ", ' ');
        await AssertValue("xyz", 'x');
        await AssertValue("\"quoted\"", "\"quoted\"");
        await AssertValue("null", "null");
    }

    [Theory]
    [InlineData("iso-8859-1", "café", true)]
    [InlineData("iso-8859-1", "café", false)]
    [InlineData("utf-16", "中文 café", true)]
    [InlineData("utf-16", "中文 café", false)]
    public async Task ResponseCharsetIsHonored(string charset, string text, bool success)
    {
        using var content = new StringContent(text, Encoding.GetEncoding(charset), "text/plain");

        var result = await content.ReadAsync<string>(success);

        Assert.Equal(text, success ? result.Item1 : result.Item2);
    }

    [Fact]
    public async Task JsonUsesDeclaredCharset()
    {
        using var content = new StringContent("{\"name\":\"café\"}", Encoding.Latin1, "application/json");

        var result = await content.ReadAsync<Dictionary<string, string>>(true);

        Assert.Equal("café", result.Item1["name"]);
    }

    [Theory]
    [InlineData("42.0")]
    [InlineData("0x10")]
    [InlineData("\"42\"")]
    [InlineData("1,000")]
    public async Task IntegerParsingKeepsStrictInputValidation(string text)
    {
        using var content = new StringContent(text);
        await Assert.ThrowsAsync<FormatException>(() => content.ReadAsync<int>(true));
    }

    [Fact]
    public async Task EnumsContinueToUseJsonConversion()
    {
        await AssertValue("1", SampleState.Ready);
        await AssertValue<SampleState?>("\"Ready\"", SampleState.Ready);
    }

    [Fact]
    public async Task NullableCustomConverterReceivesOriginalJson()
    {
        using var content = new StringContent("\"token\"");

        var result = await content.ReadAsync<EncodedToken?>(true);

        Assert.Equal("converted:token", result.Item1!.Value.Value);
    }

    private enum SampleState { Unknown, Ready }

    [Newtonsoft.Json.JsonConverter(typeof(EncodedTokenConverter))]
    private readonly struct EncodedToken
    {
        public EncodedToken(string value) => Value = value;
        public string Value { get; }
    }

    private sealed class EncodedTokenConverter : Newtonsoft.Json.JsonConverter<EncodedToken>
    {
        public EncodedTokenConverter() { }

        public override EncodedToken ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, EncodedToken existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
            => new("converted:" + reader.Value);

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, EncodedToken value, Newtonsoft.Json.JsonSerializer serializer)
            => writer.WriteValue(value.Value);
    }

    private static async Task AssertValue<T>(string text, T expected)
    {
        using var content = new StringContent(text);
        var result = await content.ReadAsync<T>(true);
        Assert.Equal(expected, result.Item1);
        Assert.Empty(result.Item2);
    }
}
