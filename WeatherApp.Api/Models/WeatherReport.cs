using System.Globalization;

namespace WeatherApp.Api.Models;

public enum WeatherEntryStatus
{
    Ok,
    InvalidDate,
    NoData,
    ApiError
}

/// <summary>Response body for GET /api/weather.</summary>
public sealed record WeatherReport(string Location, IReadOnlyList<WeatherEntry> Entries);

/// <summary>One line from dates.txt and what happened to it.</summary>
public sealed record WeatherEntry(
    string Input,
    string? Date,
    double? MinTemperature,
    double? MaxTemperature,
    double? Precipitation,
    string? TemperatureUnit,
    string? PrecipitationUnit,
    WeatherEntryStatus Status,
    bool FromCache,
    string? Error)
{
    public static WeatherEntry FromWeather(string input, DailyWeather weather, bool fromCache) =>
        new(input,
            weather.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            weather.MinTemperature,
            weather.MaxTemperature,
            weather.Precipitation,
            weather.TemperatureUnit,
            weather.PrecipitationUnit,
            WeatherEntryStatus.Ok,
            fromCache,
            null);

    public static WeatherEntry Failure(string input, string? date, WeatherEntryStatus status, string error) =>
        new(input, date, null, null, null, null, null, status, false, error);
}