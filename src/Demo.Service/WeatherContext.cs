using Microsoft.EntityFrameworkCore;

namespace Demo.Service;

public class WeatherContext : DbContext
{
    public WeatherContext(DbContextOptions<WeatherContext> context) : base(context) { }

    public DbSet<WeatherServiceRequest> WeatherServiceRequests { get; set; } = null!;
}
