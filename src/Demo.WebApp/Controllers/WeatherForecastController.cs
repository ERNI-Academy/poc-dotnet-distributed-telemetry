using Demo.Shared.Messages;
using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApp.Controllers;
[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherForecastController> _logger; 
    private readonly MassTransit.IPublishEndpoint _publishEndpoint;

    public WeatherForecastController(ILogger<WeatherForecastController> logger,
                                     HttpClient httpClient,
                                     MassTransit.IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _httpClient = httpClient;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet]
    public async Task<string> Get(CancellationToken cancellationToken)
    {
        // Using high performance logging pattern, see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/loggermessage
        Log.Warning.WebAppForecastRequestForwarded(_logger, null);
        var result = await _httpClient.GetStringAsync("https://localhost:44301/WeatherForecast", cancellationToken);
        await _publishEndpoint.Publish<WeatherMessage>(new { Note = "Demo Message" }, cancellationToken);
        return result;
    }
}
