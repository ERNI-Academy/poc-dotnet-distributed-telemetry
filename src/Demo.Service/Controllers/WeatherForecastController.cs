using Microsoft.AspNetCore.Mvc;

namespace Demo.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;
    private readonly WeatherContext _weatherContext;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, WeatherContext weatherContext)
    {
        _weatherContext = weatherContext;
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        Log.Warning.ServiceForecastRequest(_logger, null);
        _weatherContext.WeatherServiceRequests.Add(new WeatherServiceRequest() { Note = "Demo Note" });
        _weatherContext.SaveChanges();
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}
