using Client.Models;
using Dec.DiscordIPC.Commands;

namespace Client.Services;

public sealed class PlayerTrackerService : IReportingService
{
    public bool                 Started                 { get; private set; }
    public BindingSource        BindableClientPositions { get; } = new();
    public List<ClientPosition> Clients                 { get; } = [];

    private IProgress<LogItem>?         Progress                   { get; set; }
    private DiscordPipeService?       DiscordNamedPipeService    { get; set; }
    private WebSocketClientService?     WebSocketClientService     { get; set; }
    public  FactorioFileWatcherService? FactorioFileWatcherService { get; set; }

    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken = default)
    {
        Progress = progress;

        DiscordNamedPipeService    = Program.GetService<DiscordPipeService>();
        WebSocketClientService     = Program.GetService<WebSocketClientService>();
        FactorioFileWatcherService = Program.GetService<FactorioFileWatcherService>();

        UpdateDataGrid(true);

        if (WebSocketClientService != null)
        {
            WebSocketClientService.AnyClientUpdateReceived += OnClientUpdateReceived;
            WebSocketClientService.AnyClientDisconnected   += OnClientDisconnected;
        }

        Started = true;
        return Task.CompletedTask;
    }

    private void UpdateDataGrid(bool resetDataSource)
    {
        if (Main.uiThreadControl.IsHandleCreated)
        {
            if (Main.uiThreadControl.InvokeRequired)
                Main.uiThreadControl.Invoke(() =>
                {
                    if (resetDataSource)
                        BindableClientPositions.DataSource = Clients;
                    BindableClientPositions.ResetBindings(false);
                });
            else
            {
                BindableClientPositions.DataSource = Clients;
                BindableClientPositions.ResetBindings(false);
            }
        }
    }

    private void OnClientUpdateReceived(string discordId, FactorioPosition position)
    {
        var index = Clients.FindIndex(c => c.DiscordId == discordId);
        if (index == -1)
        {
            Clients.Add(new ClientPosition
            {
                DiscordId    = discordId,
                X            = position.x,
                Y            = position.y,
                SurfaceIndex = position.surfaceIndex,
            });

            Progress?.Report(new LogItem($"Client {discordId} has joined the game.", LogItem.LogType.Info));
        }
        else
        {
            var clientPosition = Clients[index];
            clientPosition.X            = position.x;
            clientPosition.Y            = position.y;
            clientPosition.SurfaceIndex = position.surfaceIndex;
        }

        if (FactorioFileWatcherService?.LastPositionPacket != null)
        {
            // TODO)) Update all client positions when local player moves to a different surface.
            CalculatePan(FactorioFileWatcherService.LastPositionPacket.Value, position, out var left, out var right);
            DiscordNamedPipeService?.SetUserVoiceSettings(discordId, new SetUserVoiceSettings.Pan
            {
                left  = left,
                right = right
            });
        }

        UpdateDataGrid(false);
    }

    public static void CalculatePan(FactorioPosition localPosition, FactorioPosition position, out float left, out float right)
    {
        left  = 0;
        right = 0;
        if (localPosition.surfaceIndex != position.surfaceIndex)
            return;

        const double falloffRadiusSqr = 100.0 * 100.0;
        const double earOffset        = 0.5;

        var leftEarDistanceSqr  = DistanceSqr(localPosition.x, localPosition.y, position.x - earOffset, position.y);
        var rightEarDistanceSqr = DistanceSqr(localPosition.x, localPosition.y, position.x + earOffset, position.y);

        left  = Proximity(leftEarDistanceSqr,  falloffRadiusSqr);
        right = Proximity(rightEarDistanceSqr, falloffRadiusSqr);
    }

    private static double DistanceSqr(double x1, double y1, double x2, double y2)
    {
        return (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
    }

    private static float Proximity(double leftEarDistanceSqr, double falloffRadiusSqr)
    {
        if (leftEarDistanceSqr > falloffRadiusSqr)
            return 0f;

        return (float)(1 - (falloffRadiusSqr - leftEarDistanceSqr) / falloffRadiusSqr);
    }

    private void OnClientDisconnected(string discordId)
    {
        var index = Clients.FindIndex(c => c.DiscordId == discordId);

        if (index == -1)
            return;

        Clients.RemoveAt(index);
        Progress?.Report(new LogItem($"Client {discordId} disconnected.", LogItem.LogType.Info));
        UpdateDataGrid(false);
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken = default)
    {
        if (WebSocketClientService != null)
        {
            WebSocketClientService.AnyClientUpdateReceived -= OnClientUpdateReceived;
            WebSocketClientService.AnyClientDisconnected   -= OnClientDisconnected;
        }

        Started = false;
        return Task.CompletedTask;
    }
}
