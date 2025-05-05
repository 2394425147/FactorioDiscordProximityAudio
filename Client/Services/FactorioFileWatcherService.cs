using Client.Models;
using Serilog;

namespace Client.Services;

public sealed class FactorioFileWatcherService : IService
{
    public event OnPositionUpdatedDelegate? OnPositionUpdated;
    public ClientPosition?                  LastKnownPosition { get; private set; }

    private string             TargetFolderFullPath { get; set; }
    private string             TargetFileFullPath   { get; set; }
    private FileSystemWatcher? FileSystemWatcher    { get; set; }

    private const string TargetFileName = "fdpa-comm";

    public delegate void OnPositionUpdatedDelegate();

    public FactorioFileWatcherService()
    {
        TargetFolderFullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                            "Factorio", "script-output");
        TargetFileFullPath = Path.Combine(TargetFolderFullPath, TargetFileName);
    }

    public async Task<bool> StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            if (FileSystemWatcher == null)
            {
                var fileStream = File.Open(TargetFileFullPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);

                if (!fileStream.CanRead)
                {
                    Log.Error("Factorio communicator file is readonly.");
                    return false;
                }

                await fileStream.DisposeAsync();

                FileSystemWatcher = new FileSystemWatcher(TargetFolderFullPath, TargetFileName)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter          = NotifyFilters.LastWrite,
                    EnableRaisingEvents   = true
                };

                FileSystemWatcher.Changed += OnFileSystemChanged;
                Log.Information("Monitoring {S}.", TargetFileFullPath);
            }
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while starting Factorio file watcher: {Message}", e.Message);
            return false;
        }

        return true;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        if (!File.Exists(TargetFileFullPath))
            return;

        try
        {
            FileSystemWatcher!.EnableRaisingEvents = false;

            using var reader = new StreamReader(TargetFileFullPath, new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode   = FileMode.Open,
                Share  = FileShare.ReadWrite
            });

            using var file         = File.Open(TargetFileFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var binaryReader = new BinaryReader(file);

            if (file.Length != 24)
                return;

            var x            = binaryReader.ReadDouble();
            var y            = binaryReader.ReadDouble();
            var surfaceIndex = binaryReader.ReadInt32();

            var position = new ClientPosition { x = x, y = y, surfaceIndex = surfaceIndex };

            if (LastKnownPosition?.Equals(position) ?? false)
                return;

            LastKnownPosition = position;
            OnPositionUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error while reading Factorio file: {Message}", ex.Message);
        }
        finally
        {
            FileSystemWatcher!.EnableRaisingEvents = true;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("Terminated Factorio file watcher.");
        return Task.CompletedTask;
    }
}
