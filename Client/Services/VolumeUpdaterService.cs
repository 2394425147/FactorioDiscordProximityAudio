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

    private void OnClientUpdateReceived(string discordId, FactorioPosition position)
    {
        Clients[discordId] = new ClientPosition
        {
            DiscordId = discordId,
            Position  = position
        };

        if (FactorioFileWatcher?.LastPositionPacket == null)
            return;

        var volume = CalculateVolume(FactorioFileWatcher.LastPositionPacket.Value, position);
        DiscordPipe?.SetUserVolume(discordId, volume);
    }

    private void OnLocalPositionUpdated(FactorioPosition obj)
    {
        foreach (var client in Clients.Values)
        {
            var volume = CalculateVolume(obj, client.Position);
            DiscordPipe?.SetUserVolume(client.DiscordId, volume);
        }
    }

    public static float CalculateVolume(FactorioPosition localPosition, FactorioPosition position)
    {
        if (localPosition.surfaceIndex != position.surfaceIndex)
            return 0;

        const double falloffRadius = 100.0;

        var leftEarDistance = Distance(localPosition.x, localPosition.y, position.x, position.y);
        return Volume(leftEarDistance, falloffRadius);
    }

    private static double Distance(double x1, double y1, double x2, double y2) =>
        Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

    private static float Volume(double leftEarDistance, double falloffRadius)
    {
        if (leftEarDistance > falloffRadius)
            return 0f;

        return (float)(1 - leftEarDistance / falloffRadius);
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
