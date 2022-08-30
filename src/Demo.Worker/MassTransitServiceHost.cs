
using MassTransit;

namespace Demo.Worker;

public class MassTransitServiceHost : IHostedService
{
    private readonly IBusControl _bus;

    public MassTransitServiceHost(IBusControl bus)
    {
        _bus = bus;
    }

    public Task StartAsync(CancellationToken cancellationToken) => _bus.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => _bus.StopAsync(cancellationToken);
}