namespace Client.Services;

public interface IService
{
    public Task       StartAsync(IServiceProvider services, CancellationToken cancellationToken = default);
    public Task       StopAsync(CancellationToken  cancellationToken = default);
}
