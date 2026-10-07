using System.ComponentModel.DataAnnotations;

namespace WeatherApp.Api.Configuration;

/// <summary>File locations, relative to the API's content root unless absolute.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string DatesFile { get; set; } = "dates.txt";

    [Required]
    public string CacheDirectory { get; set; } = "weather-data";
}