using Microsoft.Extensions.Logging.Abstractions;
using WeatherApp.Api.Models;
using WeatherApp.Api.Services;

namespace WeatherApp.Tests;

public sealed class FileWeatherCacheTests : IDisposable
{
    private static readonly DailyWeather Sample =
        new(new DateOnly(2021, 2, 27), 50.2, 71.3, 0.12, "°F", "inch");

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "weather-cache-tests", Guid.NewGuid().ToString("N"));

    private readonly FileWeatherCache _cache;

    public FileWeatherCacheTests()
    {
        _cache = new FileWeatherCache(_directory, NullLogger<FileWeatherCache>.Instance);
    }

    [Fact]
    public async Task TryGet_WhenNoFile_ReturnsNull()
    {
        Assert.Null(await _cache.TryGetAsync(Sample.Date));
    }

    [Fact]
    public async Task SaveThenTryGet_RoundTripsAllValues()
    {
        await _cache.SaveAsync(Sample);

        var loaded = await _cache.TryGetAsync(Sample.Date);

        Assert.Equal(Sample, loaded); // records compare by value
    }

    [Fact]
    public async Task Save_WritesFileNamedByIsoDate()
    {
        await _cache.SaveAsync(Sample);

        Assert.True(File.Exists(Path.Combine(_directory, "2021-02-27.json")));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp")); // no temp files left behind
    }

    [Fact]
    public async Task TryGet_WhenFileIsCorrupt_ReturnsNullInsteadOfThrowing()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_cache.GetFilePath(Sample.Date), "{ not valid json");

        Assert.Null(await _cache.TryGetAsync(Sample.Date));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}