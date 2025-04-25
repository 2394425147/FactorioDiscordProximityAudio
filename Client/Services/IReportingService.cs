namespace Client.Services;

public interface IReportingService
{
    public bool Started { get; }
    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken = default);
    public Task StopClient(IProgress<LogItem>  progress, CancellationToken cancellationToken = default);
}
