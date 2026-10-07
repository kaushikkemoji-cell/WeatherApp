using System.Text.Json.Serialization;

namespace WeatherApp.Api.Models;

public sealed class OpenMeteoResponse
{
    [JsonPropertyName("daily")]
    public OpenMeteoDaily? Daily { get; init; }

    [JsonPropertyName("daily_units")]
    public OpenMeteoDailyUnits? DailyUnits { get; init; }
}

public sealed class OpenMeteoDaily
{
    [JsonPropertyName("time")]
    public List<string>? Time { get; init; }

    [JsonPropertyName("temperature_2m_min")]
    public List<double?>? TemperatureMin { get; init; }

    [JsonPropertyName("temperature_2m_max")]
    public List<double?>? TemperatureMax { get; init; }

    [JsonPropertyName("precipitation_sum")]
    public List<double?>? PrecipitationSum { get; init; }
}

public sealed class OpenMeteoDailyUnits
{
    [JsonPropertyName("temperature_2m_max")]
    public string? TemperatureMax { get; init; }

    [JsonPropertyName("precipitation_sum")]
    public string? PrecipitationSum { get; init; }
}

/// <summary>Error body Open-Meteo returns with HTTP 400, e.g. a date outside the archive range.</summary>
public sealed class OpenMeteoErrorResponse
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}