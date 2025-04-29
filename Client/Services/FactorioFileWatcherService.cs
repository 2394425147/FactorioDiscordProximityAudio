using Client.Models;
using Serilog;

namespace Client.Services;

public sealed class FactorioFileWatcherService : IService
{
    public bool                            Started { get; private set; }
    public event Action<FactorioPosition>? OnPositionUpdated;
    public FactorioPosition?               LastPositionPacket { get; private set; }

    private string?            TargetFolderFullPath { get; set; }
    private string?            TargetFileFullPath   { get; set; }
    private FileSystemWatcher? FileSystemWatcher    { get; set; }

    private const string TargetFileName = "fdpa-comm";

    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            TargetFolderFullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                                "Factorio", "script-output");
            TargetFileFullPath = Path.Combine(TargetFolderFullPath, TargetFileName);

            var fileStream = File.Open(TargetFileFullPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);

            if (!fileStream.CanRead)
            {
                Log.Error("Factorio communicator file is readonly.");
                return;
            }

            await fileStream.DisposeAsync();

            Log.Information("Watching: {S}...", TargetFileFullPath);

            if (FileSystemWatcher == null)
            {
                FileSystemWatcher = new FileSystemWatcher(TargetFolderFullPath, TargetFileName)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter          = NotifyFilters.LastWrite,
                    EnableRaisingEvents   = true
                };

                FileSystemWatcher.Changed += OnFileSystemChanged;
            }

            Started = true;
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while starting Factorio file watcher: {Message}", e.Message);
            throw;
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        if (TargetFileFullPath == null || !File.Exists(TargetFileFullPath))
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

            var packet = new FactorioPosition { x = x, y = y, surfaceIndex = surfaceIndex };

            if (LastPositionPacket?.Equals(packet) ?? false)
                return;

            LastPositionPacket = packet;
            OnPositionUpdated?.Invoke(packet);
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
        Started = false;
        return Task.CompletedTask;
    }
}
