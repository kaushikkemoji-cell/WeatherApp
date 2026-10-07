using Microsoft.Extensions.Options;
using WeatherApp.Api.Configuration;
using WeatherApp.Api.Models;

namespace WeatherApp.Api.Services;

public interface IWeatherService
{
    Task<WeatherReport> GetWeatherAsync(CancellationToken cancellationToken = default);
}

/// <summary>Orchestrates: read dates -> parse -> cache lookup -> API call -> cache write.</summary>
public sealed class WeatherService : IWeatherService
{
    private readonly IDatesSource _datesSource;
    private readonly IDateParser _dateParser;
    private readonly IWeatherCache _cache;
    private readonly IOpenMeteoClient _client;
    private readonly OpenMeteoOptions _options;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(
        IDatesSource datesSource,
        IDateParser dateParser,
        IWeatherCache cache,
        IOpenMeteoClient client,
        IOptions<OpenMeteoOptions> options,
        ILogger<WeatherService> logger)
    {
        _datesSource = datesSource;
        _dateParser = dateParser;
        _cache = cache;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WeatherReport> GetWeatherAsync(CancellationToken cancellationToken = default)
    {
        var lines = await _datesSource.ReadLinesAsync(cancellationToken);
        var entries = new List<WeatherEntry>(lines.Count);

        // Sequential on purpose: the input is small, and it keeps us polite to a free public API.
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // tolerate blank lines and trailing newlines
            }

            entries.Add(await BuildEntryAsync(line, cancellationToken));
        }

        return new WeatherReport(_options.LocationName, entries);
    }

    private async Task<WeatherEntry> BuildEntryAsync(string line, CancellationToken cancellationToken)
    {
        var parsed = _dateParser.Parse(line);
        if (!parsed.IsValid)
        {
            _logger.LogWarning("Invalid date input {Input}: {Error}", parsed.RawInput, parsed.Error);
            return WeatherEntry.Failure(parsed.RawInput, null, WeatherEntryStatus.InvalidDate, parsed.Error!);
        }

        var date = parsed.Date!.Value;

        var cached = await _cache.TryGetAsync(date, cancellationToken);
        if (cached is not null)
        {
            return WeatherEntry.FromWeather(parsed.RawInput, cached, fromCache: true);
        }

        var result = await _client.GetDailyWeatherAsync(date, cancellationToken);

        switch (result.Status)
        {
            case WeatherFetchStatus.Success:
                // Only successful results are cached: failures may succeed on retry,
                // and recent dates may gain data later.
                await _cache.SaveAsync(result.Weather!, cancellationToken);
                return WeatherEntry.FromWeather(parsed.RawInput, result.Weather!, fromCache: false);

            case WeatherFetchStatus.NoData:
                return WeatherEntry.Failure(parsed.RawInput, parsed.IsoDate, WeatherEntryStatus.NoData,
                    result.Error ?? "No weather data is available for this date.");

            default:
                return WeatherEntry.Failure(parsed.RawInput, parsed.IsoDate, WeatherEntryStatus.ApiError,
                    result.Error ?? "The weather API request failed.");
        }
    }
}