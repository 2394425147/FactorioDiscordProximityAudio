using Client.Models;
using Client.Models.Discord;

namespace Client.Services;

public sealed class PlayerTrackerService : IReportingService
{
    public bool                 Started                 { get; private set; }
    public BindingSource        BindableClientPositions { get; } = new();
    public List<ClientPosition> Clients                 { get; } = [];

    private IProgress<LogItem>?         Progress                   { get; set; }
    private DiscordNamedPipeService?    DiscordNamedPipeService    { get; set; }
    private WebSocketClientService?     WebSocketClientService     { get; set; }
    public  FactorioFileWatcherService? FactorioFileWatcherService { get; set; }

    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken = default)
    {
        Progress = progress;

        DiscordNamedPipeService    = Program.GetService<DiscordNamedPipeService>();
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
            var pan = Pan.Calculate(FactorioFileWatcherService.LastPositionPacket.Value, position);
            DiscordNamedPipeService?.SetUserVoiceSettings(Progress!, discordId, pan,
                                                          Program.applicationExitCancellationToken?.Token ??
                                                          CancellationToken.None);
        }

        UpdateDataGrid(false);
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
