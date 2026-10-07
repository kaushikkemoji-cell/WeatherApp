using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WeatherApp.Api.Configuration;
using WeatherApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Enums serialize as "Ok" / "InvalidDate" rather than 0 / 1, which is friendlier for the UI.
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// ---- Configuration: bound and validated at startup so bad config fails fast ----
builder.Services.AddOptions<OpenMeteoOptions>()
    .Bind(builder.Configuration.GetSection(OpenMeteoOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---- Services ----
builder.Services.AddSingleton<IDateParser, DateParser>();

// Relative paths resolve against the content root (the API project folder when run from VS).
builder.Services.AddSingleton<IDatesSource>(sp =>
{
    var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var environment = sp.GetRequiredService<IHostEnvironment>();
    return new FileDatesSource(Path.Combine(environment.ContentRootPath, storage.DatesFile));
});

builder.Services.AddSingleton<IWeatherCache>(sp =>
{
    var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var environment = sp.GetRequiredService<IHostEnvironment>();
    var directory = Path.Combine(environment.ContentRootPath, storage.CacheDirectory);
    return new FileWeatherCache(directory, sp.GetRequiredService<ILogger<FileWeatherCache>>());
});

// Typed client via IHttpClientFactory: pooled handlers avoid socket exhaustion and stale DNS.
builder.Services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddScoped<IWeatherService, WeatherService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Unhandled exceptions become a ProblemDetails response instead of a stack trace.
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();