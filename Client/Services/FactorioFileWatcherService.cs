namespace Client.Services;

public sealed class FactorioFileWatcherService : IReportingService
{
    public bool Started { get; private set; }

    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Started = true;
        return Task.CompletedTask;
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Started = false;
        return Task.CompletedTask;
    }
}
