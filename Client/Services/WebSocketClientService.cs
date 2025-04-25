namespace Client.Services;

public sealed class WebSocketClientService(string address, int port) : IReportingService
{
    public bool Started { get; private set; }

    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Started = true;
        progress.Report(new LogItem($"Connecting to {address}:{port}...", LogItem.LogType.Info));
        return Task.CompletedTask;
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Started = true;
        return Task.CompletedTask;
    }
}
