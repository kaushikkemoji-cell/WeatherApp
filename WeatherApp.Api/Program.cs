using Microsoft.Extensions.Options;
using WeatherApp.Api.Configuration;
using WeatherApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bind and validate settings at startup so bad config fails fast, not on the first request.
builder.Services.AddOptions<OpenMeteoOptions>()
    .Bind(builder.Configuration.GetSection(OpenMeteoOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IDateParser, DateParser>();

// Typed client via IHttpClientFactory: pooled handlers avoid socket exhaustion and stale DNS.
builder.Services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();