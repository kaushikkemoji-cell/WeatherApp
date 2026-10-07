using Microsoft.AspNetCore.Mvc;
using WeatherApp.Api.Models;
using WeatherApp.Api.Services;

namespace WeatherApp.Api.Controllers;

[ApiController]
[Route("api/weather")]
public sealed class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(IWeatherService weatherService, ILogger<WeatherController> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }

    /// <summary>Returns weather for every date in dates.txt, with a status and error message per date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(WeatherReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WeatherReport>> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _weatherService.GetWeatherAsync(cancellationToken));
        }
        catch (FileNotFoundException ex)
        {
            // Log the real path for operators, but don't leak server file paths to clients.
            _logger.LogError(ex, "Dates file is missing: {Path}", ex.FileName);
            return Problem(
                title: "Dates file not found",
                detail: "The configured dates file could not be found on the server.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}