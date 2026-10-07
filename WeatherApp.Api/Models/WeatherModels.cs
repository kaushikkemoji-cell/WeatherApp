namespace WeatherApp.Api.Models;

/// <summary>One day of weather for the configured location. This is also what we cache to disk.</summary>
public sealed record DailyWeather(
    DateOnly Date,
    double? MinTemperature,
    double? MaxTemperature,
    double? Precipitation,
    string? TemperatureUnit,
    string? PrecipitationUnit);

public enum WeatherFetchStatus
{
    Success,
    NoData,
    Failed
}

/// <summary>Outcome of a single API call. Expected failures are values, not exceptions.</summary>
public sealed record WeatherFetchResult(WeatherFetchStatus Status, DailyWeather? Weather, string? Error)
{
    public static WeatherFetchResult Success(DailyWeather weather) => new(WeatherFetchStatus.Success, weather, null);
    public static WeatherFetchResult NoData(string reason) => new(WeatherFetchStatus.NoData, null, reason);
    public static WeatherFetchResult Failed(string reason) => new(WeatherFetchStatus.Failed, null, reason);
}