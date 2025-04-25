namespace Client.Services;

public sealed class FactorioFileWatcherService : IReportingService
{
    public  bool                Started              { get; private set; }
    private string?             TargetFolderFullPath { get; set; }
    private string?             TargetFileFullPath   { get; set; }
    private IProgress<LogItem>? Progress             { get; set; }
    private FileSystemWatcher?  FileSystemWatcher    { get; set; }

    private const string TargetFileName = "fdpa-comm";

    public async Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        try
        {
            Progress = progress;
            TargetFolderFullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                                "Factorio", "script-output");
            TargetFileFullPath = Path.Combine(TargetFolderFullPath, TargetFileName);

            var fileStream = File.Open(TargetFileFullPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);

            if (!fileStream.CanRead)
            {
                progress.Report(new LogItem("Factorio communicator file is readonly.", LogItem.LogType.Error));
                return;
            }

            await fileStream.DisposeAsync();

            progress.Report(new LogItem($"Watching: {TargetFileFullPath}...", LogItem.LogType.Info));

            FileSystemWatcher = new FileSystemWatcher(TargetFolderFullPath, TargetFileName)
            {
                IncludeSubdirectories = false,
                NotifyFilter          = NotifyFilters.LastWrite,
                EnableRaisingEvents   = true
            };

            FileSystemWatcher.Changed += OnFileSystemChanged;
            Started                   =  true;
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
            throw;
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        FileSystemWatcher!.EnableRaisingEvents = false;

        if (e.ChangeType != WatcherChangeTypes.Changed)
        {
            FileSystemWatcher!.EnableRaisingEvents = true;
            return;
        }

        if (TargetFileFullPath == null || !File.Exists(TargetFileFullPath))
            return;

        using var reader = new StreamReader(TargetFileFullPath, new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode   = FileMode.Open,
            Share  = FileShare.ReadWrite
        });

        var x            = float.Parse(reader.ReadLine() ?? string.Empty);
        var y            = float.Parse(reader.ReadLine() ?? string.Empty);
        var index        = int.Parse(reader.ReadLine()   ?? string.Empty);
        var surfaceIndex = int.Parse(reader.ReadLine()   ?? string.Empty);
        var playerName   = reader.ReadLine();

        Progress?.Report(new LogItem($"{playerName}: ({x:F2}, {y:F2}) on surface {surfaceIndex}",
                                     LogItem.LogType.Info));

        FileSystemWatcher!.EnableRaisingEvents = true;
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        FileSystemWatcher?.Dispose();

        Started = false;
        return Task.CompletedTask;
    }
}
