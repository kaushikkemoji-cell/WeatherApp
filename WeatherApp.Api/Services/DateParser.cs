using System.Globalization;

namespace WeatherApp.Api.Services;

/// <summary>Result of parsing one raw line from dates.txt.</summary>
public sealed record DateParseResult(string RawInput, DateOnly? Date, string? Error)
{
    public bool IsValid => Date.HasValue;

    /// <summary>Normalized ISO 8601 date (yyyy-MM-dd), or null when invalid.</summary>
    public string? IsoDate => Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public interface IDateParser
{
    DateParseResult Parse(string? input);
}

public sealed class DateParser : IDateParser
{
    // Single-letter specifiers (M, d) accept both "2" and "02" when parsing,
    // so one pattern covers padded and unpadded values.
    private static readonly string[] SupportedFormats =
    {
        "M/d/yyyy",      // 02/27/2021
        "MMMM d, yyyy",  // June 2, 2022
        "MMM-d-yyyy",    // Jul-13-2020
        "yyyy-MM-dd",    // already ISO
    };

    public DateParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new DateParseResult(input ?? string.Empty, null, "Date value is empty.");
        }

        var trimmed = input.Trim();

        // TryParseExact validates against the real calendar, so impossible
        // dates such as April 31 or Feb 29 in a non-leap year are rejected.
        if (DateOnly.TryParseExact(
                trimmed,
                SupportedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return new DateParseResult(trimmed, date, null);
        }

        return new DateParseResult(
            trimmed,
            null,
            $"'{trimmed}' is not a valid calendar date in a supported format.");
    }
}