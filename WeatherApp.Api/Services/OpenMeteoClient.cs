using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WeatherApp.Api.Configuration;
using WeatherApp.Api.Models;

namespace WeatherApp.Api.Services;

public interface IOpenMeteoClient
{
    Task<WeatherFetchResult> GetDailyWeatherAsync(DateOnly date, CancellationToken cancellationToken = default);
}

public sealed class OpenMeteoClient : IOpenMeteoClient
{
    private const string DailyFields = "temperature_2m_min,temperature_2m_max,precipitation_sum";

    private readonly HttpClient _httpClient;
    private readonly OpenMeteoOptions _options;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(
        HttpClient httpClient,
        IOptions<OpenMeteoOptions> options,
        ILogger<OpenMeteoClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WeatherFetchResult> GetDailyWeatherAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var isoDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var requestUri = BuildRequestUri(isoDate);

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var reason = await ReadErrorReasonAsync(response, cancellationToken);
                _logger.LogWarning(
                    "Open-Meteo returned {StatusCode} for {Date}: {Reason}",
                    (int)response.StatusCode, isoDate, reason);
                return WeatherFetchResult.Failed($"Weather API returned HTTP {(int)response.StatusCode}: {reason}");
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenMeteoResponse>(
                cancellationToken: cancellationToken);

            return MapToResult(date, isoDate, payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient signals a timeout as a cancellation. If the caller did not
            // cancel, it was our timeout, so report it rather than rethrowing.
            _logger.LogWarning("Open-Meteo request timed out for {Date}", isoDate);
            return WeatherFetchResult.Failed("Weather API request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach Open-Meteo for {Date}", isoDate);
            return WeatherFetchResult.Failed("Could not reach the weather API.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Open-Meteo returned malformed JSON for {Date}", isoDate);
            return WeatherFetchResult.Failed("Weather API returned a response in an unexpected format.");
        }
    }

    private string BuildRequestUri(string isoDate)
    {
        // InvariantCulture matters: on a machine whose culture uses a comma decimal
        // separator, 32.78 would otherwise be sent as "32,78".
        var query = new Dictionary<string, string>
        {
            ["latitude"] = _options.Latitude.ToString(CultureInfo.InvariantCulture),
            ["longitude"] = _options.Longitude.ToString(CultureInfo.InvariantCulture),
            ["start_date"] = isoDate,
            ["end_date"] = isoDate,
            ["daily"] = DailyFields,
            ["timezone"] = _options.Timezone,
            ["temperature_unit"] = _options.TemperatureUnit,
            ["precipitation_unit"] = _options.PrecipitationUnit,
        };

        return "v1/archive?" + string.Join("&",
            query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static WeatherFetchResult MapToResult(DateOnly date, string isoDate, OpenMeteoResponse? payload)
    {
        var daily = payload?.Daily;
        var index = daily?.Time?.IndexOf(isoDate) ?? -1;

        if (daily is null || index < 0)
        {
            return WeatherFetchResult.NoData("Weather API returned no daily data for this date.");
        }

        var min = ValueAt(daily.TemperatureMin, index);
        var max = ValueAt(daily.TemperatureMax, index);
        var precipitation = ValueAt(daily.PrecipitationSum, index);

        if (min is null && max is null && precipitation is null)
        {
            return WeatherFetchResult.NoData("No weather observations are available for this date.");
        }

        return WeatherFetchResult.Success(new DailyWeather(
            date,
            min,
            max,
            precipitation,
            payload!.DailyUnits?.TemperatureMax,
            payload.DailyUnits?.PrecipitationSum));
    }

    private static double? ValueAt(List<double?>? values, int index) =>
        values is not null && index < values.Count ? values[index] : null;

    private static async Task<string> ReadErrorReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<OpenMeteoErrorResponse>(
                cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Reason))
            {
                return error.Reason;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Body was not the JSON error shape; fall back to the status text.
        }

        return response.ReasonPhrase ?? "Unknown error";
    }
}