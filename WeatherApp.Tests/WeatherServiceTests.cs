using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherApp.Api.Configuration;
using WeatherApp.Api.Models;
using WeatherApp.Api.Services;

namespace WeatherApp.Tests;

public class WeatherServiceTests
{
    private static readonly DailyWeather Feb27 =
        new(new DateOnly(2021, 2, 27), 50.2, 71.3, 0.12, "°F", "inch");

    private readonly FakeCache _cache = new();
    private readonly FakeClient _client = new();

    [Fact]
    public async Task InvalidDate_IsReportedAndApiIsNotCalled()
    {
        var report = await CreateService("April 31, 2022").GetWeatherAsync();

        var entry = Assert.Single(report.Entries);
        Assert.Equal(WeatherEntryStatus.InvalidDate, entry.Status);
        Assert.Equal("April 31, 2022", entry.Input);
        Assert.Null(entry.Date);
        Assert.NotNull(entry.Error);
        Assert.Empty(_client.RequestedDates);
    }

    [Fact]
    public async Task CachedDate_IsServedFromCacheWithoutCallingApi()
    {
        _cache.Items[Feb27.Date] = Feb27;

        var report = await CreateService("02/27/2021").GetWeatherAsync();

        var entry = Assert.Single(report.Entries);
        Assert.Equal(WeatherEntryStatus.Ok, entry.Status);
        Assert.True(entry.FromCache);
        Assert.Equal(50.2, entry.MinTemperature);
        Assert.Empty(_client.RequestedDates);
    }

    [Fact]
    public async Task UncachedDate_FetchesFromApiAndStoresResult()
    {
        _client.Respond = _ => WeatherFetchResult.Success(Feb27);

        var report = await CreateService("02/27/2021").GetWeatherAsync();

        var entry = Assert.Single(report.Entries);
        Assert.Equal(WeatherEntryStatus.Ok, entry.Status);
        Assert.False(entry.FromCache);
        Assert.Equal("2021-02-27", entry.Date);
        Assert.Equal(71.3, entry.MaxTemperature);
        Assert.True(_cache.Items.ContainsKey(Feb27.Date));
    }

    [Theory]
    [InlineData(WeatherFetchStatus.NoData, WeatherEntryStatus.NoData)]
    [InlineData(WeatherFetchStatus.Failed, WeatherEntryStatus.ApiError)]
    public async Task UnsuccessfulFetch_IsReportedAndNotCached(
        WeatherFetchStatus fetchStatus, WeatherEntryStatus expectedStatus)
    {
        _client.Respond = _ => new WeatherFetchResult(fetchStatus, null, "something went wrong");

        var report = await CreateService("02/27/2021").GetWeatherAsync();

        var entry = Assert.Single(report.Entries);
        Assert.Equal(expectedStatus, entry.Status);
        Assert.Equal("2021-02-27", entry.Date);
        Assert.Equal("something went wrong", entry.Error);
        Assert.Empty(_cache.Items);
    }

    [Fact]
    public async Task MixedInput_PreservesOrderAndSkipsBlankLines()
    {
        _client.Respond = date => WeatherFetchResult.Success(Feb27 with { Date = date });

        var report = await CreateService("02/27/2021", "", "April 31, 2022", "June 2, 2022")
            .GetWeatherAsync();

        Assert.Equal(3, report.Entries.Count);
        Assert.Equal(
            new[] { WeatherEntryStatus.Ok, WeatherEntryStatus.InvalidDate, WeatherEntryStatus.Ok },
            report.Entries.Select(e => e.Status));
        Assert.Equal("2022-06-02", report.Entries[2].Date);
        Assert.Equal("Dallas, TX", report.Location);
    }

    private WeatherService CreateService(params string[] lines) =>
        new(new FakeDatesSource(lines),
            new DateParser(),
            _cache,
            _client,
            Options.Create(new OpenMeteoOptions { LocationName = "Dallas, TX" }),
            NullLogger<WeatherService>.Instance);

    private sealed class FakeDatesSource(IReadOnlyList<string> lines) : IDatesSource
    {
        public Task<IReadOnlyList<string>> ReadLinesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(lines);
    }

    private sealed class FakeCache : IWeatherCache
    {
        public Dictionary<DateOnly, DailyWeather> Items { get; } = new();

        public Task<DailyWeather?> TryGetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<DailyWeather?>(Items.GetValueOrDefault(date));

        public Task SaveAsync(DailyWeather weather, CancellationToken cancellationToken = default)
        {
            Items[weather.Date] = weather;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClient : IOpenMeteoClient
    {
        public Func<DateOnly, WeatherFetchResult> Respond { get; set; } =
            _ => throw new InvalidOperationException("The weather API should not have been called.");

        public List<DateOnly> RequestedDates { get; } = new();

        public Task<WeatherFetchResult> GetDailyWeatherAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            RequestedDates.Add(date);
            return Task.FromResult(Respond(date));
        }
    }
}