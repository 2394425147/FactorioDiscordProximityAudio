using System.Runtime.CompilerServices;
using Client.Models;
using Serilog;

namespace Client.Services;

public sealed class VolumeUpdaterService : IService
{
    public Dictionary<string, ClientPosition> Clients { get; } = [];

    private DiscordPipeService?            DiscordPipe            { get; set; }
    private PositionTransferClientService? PositionTransferClient { get; set; }
    public  FactorioFileWatcherService?    FactorioFileWatcher    { get; set; }

    public Task<bool> StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        DiscordPipe            = services.GetService(typeof(DiscordPipeService)) as DiscordPipeService;
        PositionTransferClient = services.GetService(typeof(PositionTransferClientService)) as PositionTransferClientService;
        FactorioFileWatcher    = services.GetService(typeof(FactorioFileWatcherService)) as FactorioFileWatcherService;

        if (PositionTransferClient != null)
        {
            PositionTransferClient.AnyClientUpdateReceived += OnClientUpdateReceived;
            PositionTransferClient.AnyClientDisconnected   += OnClientDisconnected;
        }

        if (FactorioFileWatcher != null)
        {
            FactorioFileWatcher.OnPositionUpdated += OnLocalPositionUpdated;
        }

        Log.Information("Started volume updater.");
        return Task.FromResult(true);
    }

    private void OnClientUpdateReceived(string discordId, ClientPosition position)
    {
        Clients[discordId] = position;

        if (FactorioFileWatcher is not { LastKnownPosition : not null })
            return;

        var localPosition = FactorioFileWatcher.LastKnownPosition.Value;

        var volume = CalculateVolume(localPosition, position);
        DiscordPipe?.SetUserVolume(discordId, volume);
    }

    private void OnLocalPositionUpdated()
    {
        if (FactorioFileWatcher is not { LastKnownPosition: not null })
            return;

        var localPosition = FactorioFileWatcher.LastKnownPosition.Value;

        foreach (var (discordId, position) in Clients)
        {
            var volume = CalculateVolume(localPosition, position);
            DiscordPipe?.SetUserVolume(discordId, volume);
        }
    }

    public static float CalculateVolume(ClientPosition @this, ClientPosition other)
    {
        if (@this.surfaceIndex != other.surfaceIndex)
            return 0;

        const double falloffRadius = 100.0;

        var distance = Distance(@this.x, @this.y, other.x, other.y);

        if (distance > falloffRadius)
            return 0;

        return (float)(1 - distance / falloffRadius);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double Distance(double x1, double y1, double x2, double y2) =>
            Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
    }

    private void OnClientDisconnected(string discordId)
    {
        if (!Clients.Remove(discordId))
            return;

        DiscordPipe?.ResetUserVolume(discordId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (PositionTransferClient != null)
            {
                PositionTransferClient.AnyClientUpdateReceived -= OnClientUpdateReceived;
                PositionTransferClient.AnyClientDisconnected   -= OnClientDisconnected;
            }

            if (DiscordPipe != null)
            {
                foreach (var client in Clients)
                {
                    await DiscordPipe.ResetUserVolume(client.Key);
                }
            }

            Log.Information("Terminated volume updater.");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error terminating volume updater: {Message}", e.Message);
            throw;
        }
    }
}
