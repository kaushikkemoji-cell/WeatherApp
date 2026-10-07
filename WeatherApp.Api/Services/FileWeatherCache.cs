using System.Globalization;
using System.Text.Json;
using WeatherApp.Api.Models;

namespace WeatherApp.Api.Services;

public interface IWeatherCache
{
    Task<DailyWeather?> TryGetAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task SaveAsync(DailyWeather weather, CancellationToken cancellationToken = default);
}

/// <summary>Stores one JSON file per date, e.g. weather-data/2021-02-27.json.</summary>
public sealed class FileWeatherCache : IWeatherCache
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _directory;
    private readonly ILogger<FileWeatherCache> _logger;

    public FileWeatherCache(string directory, ILogger<FileWeatherCache> logger)
    {
        _directory = directory;
        _logger = logger;
    }

    public string GetFilePath(DateOnly date) =>
        Path.Combine(_directory, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json");

    public async Task<DailyWeather?> TryGetAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(date);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var weather = await JsonSerializer.DeserializeAsync<DailyWeather>(stream, JsonOptions, cancellationToken);

            if (weather is null || weather.Date != date)
            {
                _logger.LogWarning("Cache file {Path} has unexpected content; it will be refetched", path);
                return null;
            }

            return weather;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A corrupt or locked file is treated as a cache miss, never as a failure.
            _logger.LogWarning(ex, "Ignoring unreadable cache file {Path}; it will be refetched", path);
            return null;
        }
    }

    public async Task SaveAsync(DailyWeather weather, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(weather.Date);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(_directory);

            // Write to a temp file, then rename. The rename is atomic on the same volume,
            // so readers never see a half-written file.
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, weather, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Caching is best-effort: the caller still gets the data even if we cannot persist it.
            _logger.LogWarning(ex, "Could not write cache file {Path}", path);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}