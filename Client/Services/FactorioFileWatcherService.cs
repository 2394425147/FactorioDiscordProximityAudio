using Client.Models;

namespace Client.Services;

public sealed class FactorioFileWatcherService : IReportingService
{
    public bool                    Started { get; private set; }
    public event Action<FactorioPosition>? OnPositionUpdated;
    public FactorioPosition?               LastPositionPacket { get; private set; }

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
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        if (TargetFileFullPath == null || !File.Exists(TargetFileFullPath))
            return;

        FileSystemWatcher!.EnableRaisingEvents = false;

        using var reader = new StreamReader(TargetFileFullPath, new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode   = FileMode.Open,
            Share  = FileShare.ReadWrite
        });

        try
        {
            using var file         = File.Open(TargetFileFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var binaryReader = new BinaryReader(file);

            if (file.Length != 24)
                return;

            var x            = binaryReader.ReadDouble();
            var y            = binaryReader.ReadDouble();
            var surfaceIndex = binaryReader.ReadInt32();

            var packet = new FactorioPosition { x = x, y = y, surfaceIndex = surfaceIndex };

            if (LastPositionPacket?.Equals(packet) ?? false)
                return;

            LastPositionPacket = packet;
            OnPositionUpdated?.Invoke(packet);
        }
        catch (Exception ex)
        {
            Progress?.Report(new LogItem(ex.Message, LogItem.LogType.Error, ex.ToString()));
        }
        finally
        {
            FileSystemWatcher!.EnableRaisingEvents = true;
        }
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        FileSystemWatcher?.Dispose();

        Started = false;
        return Task.CompletedTask;
    }
}
