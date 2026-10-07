using WeatherApp.Api.Services;

namespace WeatherApp.Tests;

public class DateParserTests
{
    private readonly DateParser _parser = new();

    [Theory]
    [InlineData("02/27/2021", "2021-02-27")]
    [InlineData("June 2, 2022", "2022-06-02")]
    [InlineData("Jul-13-2020", "2020-07-13")]
    [InlineData("2/7/2021", "2021-02-07")]          // unpadded
    [InlineData("  02/27/2021  ", "2021-02-27")]    // surrounding whitespace
    [InlineData("june 2, 2022", "2022-06-02")]      // month names are case-insensitive
    [InlineData("02/29/2020", "2020-02-29")]        // leap day in a leap year
    [InlineData("2021-02-27", "2021-02-27")]        // already ISO
    public void Parse_SupportedFormats_ReturnsIsoDate(string input, string expectedIso)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid);
        Assert.Equal(expectedIso, result.IsoDate);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("April 31, 2022")]   // April has 30 days
    [InlineData("02/29/2021")]       // 2021 is not a leap year
    [InlineData("13/01/2021")]       // no month 13
    [InlineData("27/02/2021")]       // day-first is not a supported format
    [InlineData("not a date")]
    public void Parse_InvalidDates_ReturnsErrorWithoutThrowing(string input)
    {
        var result = _parser.Parse(input);

        Assert.False(result.IsValid);
        Assert.Null(result.IsoDate);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyInput_ReturnsEmptyError(string? input)
    {
        var result = _parser.Parse(input);

        Assert.False(result.IsValid);
        Assert.Equal("Date value is empty.", result.Error);
    }
}