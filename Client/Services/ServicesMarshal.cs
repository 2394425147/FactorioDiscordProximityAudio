using Serilog;

namespace Client.Services;

public sealed class ServicesMarshal(IServiceProvider serviceProvider)
{
    private readonly List<IService> _activeServices = [];
    public           bool           IsShuttingDown { get; private set; }
    public           Action?        OnStarted      { get; set; }
    public           Action?        OnStopped      { get; set; }

    public async Task StartAsync(Type[][] serviceTypes, CancellationToken ct)
    {
        var resolutionStep = serviceTypes
                             .Select(step => step.Select(serviceProvider.GetService).Cast<IService>().ToArray())
                             .ToArray();

        var startingTasks = new List<Task>();

        foreach (var step in resolutionStep)
        {
            startingTasks.Clear();

            var faulted = false;

            foreach (var service in step)
                startingTasks.Add(service.StartAsync(serviceProvider, ct)
                                         .ContinueWith(t => t.Result || (faulted = true), ct));

            _activeServices.AddRange(step);

            await Task.WhenAll(startingTasks);

            if (!ct.IsCancellationRequested && !faulted)
                continue;

            Log.Error("Error starting services. Stopping services...");
            await StopAsync();
            return;
        }

        OnStarted?.Invoke();
    }

    public async Task StopAsync()
    {
        if (IsShuttingDown)
            return;

        try
        {
            IsShuttingDown = true;
            for (var i = _activeServices.Count - 1; i >= 0; i--)
            {
                var service = _activeServices[i];
                await service.StopAsync();
            }

            _activeServices.Clear();
            OnStopped?.Invoke();
            IsShuttingDown = false;
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error stopping services: {Message}", e.Message);
        }
    }
}
