using Client.Models;

namespace Client.Services;

public sealed class PlayerTrackerService : IReportingService
{
    public bool                 Started                 { get; private set; }
    public BindingSource        BindableClientPositions { get; } = new();
    public List<ClientPosition> Clients                 { get; } = [];

    private IProgress<LogItem>?      Progress                { get; set; }
    private DiscordNamedPipeService? DiscordNamedPipeService { get; set; }
    private WebSocketClientService?  WebSocketClientService  { get; set; }

    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken = default)
    {
        Progress = progress;

        Main.uiThreadControl.Invoke(() => BindableClientPositions.DataSource = Clients);

        DiscordNamedPipeService = Program.GetService<DiscordNamedPipeService>();
        WebSocketClientService  = Program.GetService<WebSocketClientService>();

        if (WebSocketClientService != null)
        {
            WebSocketClientService.AnyClientUpdateReceived += OnClientUpdateReceived;
            WebSocketClientService.AnyClientDisconnected   += OnClientDisconnected;
        }

        Started = true;
        return Task.CompletedTask;
    }

    private void OnClientUpdateReceived(string discordId, FactorioPosition position)
    {
        BindableClientPositions.SuspendBinding();
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

            index = Clients.Count - 1;
            Progress?.Report(new LogItem($"Client {discordId} has joined the game.", LogItem.LogType.Info));
        }
        else
        {
            var clientPosition = Clients[index];
            clientPosition.X            = position.x;
            clientPosition.Y            = position.y;
            clientPosition.SurfaceIndex = position.surfaceIndex;
            Progress?.Report(new LogItem($"Client {discordId} has moved.", LogItem.LogType.Info));
        }

        BindableClientPositions.ResetItem(index);
        BindableClientPositions.ResumeBinding();
    }

    private void OnClientDisconnected(string discordId)
    {
        BindableClientPositions.SuspendBinding();
        var index = Clients.FindIndex(c => c.DiscordId == discordId);
        if (index != -1)
        {
            BindableClientPositions.RemoveAt(index);
            // Clients.RemoveAt(index);
        }
        BindableClientPositions.ResumeBinding();
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken = default)
    {
        Started = false;

        if (WebSocketClientService != null)
        {
            WebSocketClientService.AnyClientUpdateReceived -= OnClientUpdateReceived;
            WebSocketClientService.AnyClientDisconnected   -= OnClientDisconnected;
        }

        return Task.CompletedTask;
    }
}
