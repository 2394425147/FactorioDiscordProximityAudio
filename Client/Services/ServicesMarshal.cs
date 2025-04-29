using Serilog;

namespace Client.Services;

public sealed class ServicesMarshal(IServiceProvider serviceProvider)
{
    private readonly List<IService> _activeServices = [];

    public async Task StartAsync(Type[][] serviceTypes, CancellationToken ct)
    {
        var resolutionStep = serviceTypes
                             .Select(step => step.Select(serviceProvider.GetService).Cast<IService>().ToArray())
                             .ToArray();

        try
        {
            var startingTasks = new List<Task>();

            foreach (var step in resolutionStep)
            {
                startingTasks.Clear();

                foreach (var service in step)
                    startingTasks.Add(service.StartAsync(serviceProvider, ct)
                                             .ContinueWith(_ => _activeServices.Add(service), ct));

                await Task.WhenAll(startingTasks);
            }
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error starting services: {Message}", e.Message);
            await StopAsync();
            throw;
        }
    }

    public async Task StopAsync()
    {
        for (var i = _activeServices.Count - 1; i >= 0; i--)
        {
            var service = _activeServices[i];
            await service.StopAsync();
        }

        _activeServices.Clear();
    }
}
