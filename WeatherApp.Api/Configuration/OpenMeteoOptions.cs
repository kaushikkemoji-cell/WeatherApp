using System.ComponentModel.DataAnnotations;

namespace WeatherApp.Api.Configuration;

/// <summary>Settings for the Open-Meteo Historical Weather API, bound from appsettings.json.</summary>
public sealed class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://archive-api.open-meteo.com/";

    public string LocationName { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [Required]
    public string Timezone { get; set; } = "auto";

    [Required]
    public string TemperatureUnit { get; set; } = "fahrenheit";

    [Required]
    public string PrecipitationUnit { get; set; } = "inch";

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 15;
}