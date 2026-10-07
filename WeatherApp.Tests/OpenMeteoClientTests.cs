using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherApp.Api.Configuration;
using WeatherApp.Api.Models;
using WeatherApp.Api.Services;

namespace WeatherApp.Tests;

public class OpenMeteoClientTests
{
    private static readonly DateOnly TestDate = new(2021, 2, 27);

    [Fact]
    public async Task ValidResponse_MapsValuesAndUnits()
    {
        const string json = """
            {
              "daily_units": { "temperature_2m_max": "°F", "precipitation_sum": "inch" },
              "daily": {
                "time": ["2021-02-27"],
                "temperature_2m_min": [50.2],
                "temperature_2m_max": [71.3],
                "precipitation_sum": [0.12]
              }
            }
            """;
        var handler = StubHandler.Json(HttpStatusCode.OK, json);

        var result = await CreateClient(handler).GetDailyWeatherAsync(TestDate);

        Assert.Equal(WeatherFetchStatus.Success, result.Status);
        Assert.Equal(50.2, result.Weather!.MinTemperature);
        Assert.Equal(71.3, result.Weather.MaxTemperature);
        Assert.Equal(0.12, result.Weather.Precipitation);
        Assert.Equal("°F", result.Weather.TemperatureUnit);
        Assert.Contains("start_date=2021-02-27", handler.LastRequestUri!.Query);
        Assert.Contains("latitude=32.78", handler.LastRequestUri.Query);
    }

    [Fact]
    public async Task AllValuesNull_ReturnsNoData()
    {
        const string json = """
            { "daily": { "time": ["2021-02-27"], "temperature_2m_min": [null],
                         "temperature_2m_max": [null], "precipitation_sum": [null] } }
            """;

        var result = await CreateClient(StubHandler.Json(HttpStatusCode.OK, json))
            .GetDailyWeatherAsync(TestDate);

        Assert.Equal(WeatherFetchStatus.NoData, result.Status);
        Assert.Null(result.Weather);
    }

    [Fact]
    public async Task MissingDailyBlock_ReturnsNoData()
    {
        var result = await CreateClient(StubHandler.Json(HttpStatusCode.OK, "{}"))
            .GetDailyWeatherAsync(TestDate);

        Assert.Equal(WeatherFetchStatus.NoData, result.Status);
    }

    [Fact]
    public async Task BadRequest_ReturnsFailedWithApiReason()
    {
        const string json = """{ "error": true, "reason": "Parameter 'start_date' is out of allowed range" }""";

        var result = await CreateClient(StubHandler.Json(HttpStatusCode.BadRequest, json))
            .GetDailyWeatherAsync(TestDate);

        Assert.Equal(WeatherFetchStatus.Failed, result.Status);
        Assert.Contains("out of allowed range", result.Error);
    }

    [Fact]
    public async Task NetworkFailure_ReturnsFailedWithoutThrowing()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("No such host is known."));

        var result = await CreateClient(handler).GetDailyWeatherAsync(TestDate);

        Assert.Equal(WeatherFetchStatus.Failed, result.Status);
        Assert.Equal("Could not reach the weather API.", result.Error);
    }

    [Fact]
    public async Task MalformedJson_ReturnsFailed()
    {
        var result = await CreateClient(StubHandler.Json(HttpStatusCode.OK, "{ not json"))
            .GetDailyWeatherAsync(TestDate);

        Assert.Equal(WeatherFetchStatus.Failed, result.Status);
    }

    private static OpenMeteoClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://archive-api.open-meteo.com/") };
        var options = Options.Create(new OpenMeteoOptions
        {
            Latitude = 32.78,
            Longitude = -96.8,
            Timezone = "America/Chicago",
        });
        return new OpenMeteoClient(httpClient, options, NullLogger<OpenMeteoClient>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public Uri? LastRequestUri { get; private set; }

        public static StubHandler Json(HttpStatusCode status, string json) =>
            new(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_respond(request));
        }
    }
}