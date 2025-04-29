using Client.Models;

namespace Client.Services;

public sealed class PlayerTrackerService : IService
{
    public bool                               Started { get; private set; }
    public Dictionary<string, ClientPosition> Clients { get; } = [];

    private DiscordPipeService?            DiscordPipe            { get; set; }
    private PositionTransferClientService? PositionTransferClient { get; set; }
    public  FactorioFileWatcherService?    FactorioFileWatcher    { get; set; }

    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
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

        Started = true;
        return Task.CompletedTask;
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
        DiscordPipe?.SetUserVoiceSettings(discordId, volume);
    }

    private void OnLocalPositionUpdated(FactorioPosition obj)
    {
        foreach (var client in Clients.Values)
        {
            var volume = CalculateVolume(obj, client.Position);
            DiscordPipe?.SetUserVoiceSettings(client.DiscordId, volume);
        }
    }

    public static float CalculateVolume(FactorioPosition localPosition, FactorioPosition position)
    {
        if (localPosition.surfaceIndex != position.surfaceIndex)
            return 0;

        const double falloffRadiusSqr = 100.0 * 100.0;

        var leftEarDistanceSqr = DistanceSqr(localPosition.x, localPosition.y, position.x, position.y);
        return Proximity(leftEarDistanceSqr, falloffRadiusSqr) * 100f;
    }

    private static double DistanceSqr(double x1, double y1, double x2, double y2)
    {
        return (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
    }

    private static float Proximity(double leftEarDistanceSqr, double falloffRadiusSqr)
    {
        if (leftEarDistanceSqr > falloffRadiusSqr)
            return 0f;

        var result = (float)(1 - leftEarDistanceSqr / falloffRadiusSqr);
        return result;
    }

    private void OnClientDisconnected(string discordId)
    {
        if (!Clients.Remove(discordId))
            return;

        DiscordPipe?.SetUserVoiceSettings(discordId, null);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (PositionTransferClient != null)
        {
            PositionTransferClient.AnyClientUpdateReceived -= OnClientUpdateReceived;
            PositionTransferClient.AnyClientDisconnected   -= OnClientDisconnected;
        }

        Started = false;
        return Task.CompletedTask;
    }
}
