using System.Net.NetworkInformation;
using Client.Services;
using Client.VisualComponents;
using Serilog;

namespace Client;

public sealed partial class Main : Form
{
    public static string? targetIp;
    public static int     targetPort;

    private readonly ServicesMarshal _servicesMarshal;
    private          string          _connectButtonText = string.Empty;
    private          bool            _hasConnection;

    public Main(ServicesMarshal servicesMarshal)
    {
        _servicesMarshal = servicesMarshal;
        InitializeComponent();
    }

    private void Main_Load(object sender, EventArgs e)
    {
        isClient.Checked = true;
        portTextbox.Text = PositionTransferHostService.StartingPort.ToString();

        Log.Logger = new LoggerConfiguration()
                     .WriteTo.RichTextBox(logTextbox)
                     .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                     .CreateLogger();

        toolTip1.SetToolTip(ipTextbox, "IP Address of the host to connect to. Defaults to 127.0.0.1 for host mode.");
        toolTip1.SetToolTip(
            portTextbox,
            $"Port of the host to connect to. Defaults to {PositionTransferHostService.StartingPort} for host mode.");

        toolTip1.SetToolTip(isHost,   "Host to manage player positions. A client connection will also be made.");
        toolTip1.SetToolTip(isClient, "Connect to a host to send and receive player positions.");
    }

    private async void connectButton_Click(object sender, EventArgs e)
    {
        var hadConnection = _hasConnection;
        _hasConnection = !_hasConnection;

        if (hadConnection)
            await DisconnectServices();
        else
            await ConnectServices();

        ipTextbox.Enabled = portTextbox.Enabled = isClient.Enabled = isHost.Enabled = !_hasConnection;
    }

    private async Task ConnectServices()
    {
        _hasConnection = true;

        var isDestinationValid = isHost.Checked
            ? TryValidateHostWebSocketDestination(out targetIp, out targetPort)
            : TryValidateClientWebSocketDestination(out targetIp, out targetPort);

        if (!isDestinationValid)
            return;

        ipTextbox.Text   = targetIp;
        portTextbox.Text = targetPort.ToString();

        connectButton.Text    = "Connecting...";
        connectButton.Enabled = false;

        if (!isDestinationValid)
        {
            CancelConnection();
            return;
        }

        Type[][] serviceTypes = isHost.Checked
            ?
            [
                [typeof(DiscordPipeService), typeof(FactorioFileWatcherService)],
                [typeof(PositionTransferHostService)],
                [typeof(PositionTransferClientService)],
                [typeof(PlayerTrackerService)]
            ]
            :
            [
                [typeof(DiscordPipeService), typeof(FactorioFileWatcherService)],
                [typeof(PositionTransferClientService)],
                [typeof(PlayerTrackerService)]
            ];

        var task = _servicesMarshal.StartAsync(serviceTypes, Program.ApplicationExitCancellationToken);

        await Task.WhenAny(task.ContinueWith(_ => CompleteConnection(), Program.ApplicationExitCancellationToken,
                                             TaskContinuationOptions.OnlyOnRanToCompletion,
                                             TaskScheduler.FromCurrentSynchronizationContext()),
                           task.ContinueWith(_ => CancelConnection(), Program.ApplicationExitCancellationToken,
                                             TaskContinuationOptions.OnlyOnFaulted,
                                             TaskScheduler.FromCurrentSynchronizationContext()));
        return;

        void CompleteConnection()
        {
            connectButton.Text    = "Disconnect";
            connectButton.Enabled = true;
        }

        void CancelConnection()
        {
            connectButton.Text    = _connectButtonText;
            connectButton.Enabled = true;
            _hasConnection        = false;
            ipTextbox.Enabled     = portTextbox.Enabled = isClient.Enabled = isHost.Enabled = !_hasConnection;
        }
    }

    private bool TryValidateHostWebSocketDestination(out string address, out int port)
    {
        port    = 0;
        address = "127.0.0.1";

        var portsInUse = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Select(x => x.Port).ToHashSet();

        if (!int.TryParse(portTextbox.Text, out port) ||
            port is <= 0 or >= ushort.MaxValue        ||
            portsInUse.Contains(port))
        {
            Log.Warning("Port is left empty or unavailable. Finding a port...");

            var openPort =
                AddressUtility.FindFirstAvailablePort(portsInUse, PositionTransferHostService.StartingPort,
                                                      PositionTransferHostService.CheckPortCount);
            if (openPort == 0)
            {
                Log.Error("Failed to find an available port.");
                return false;
            }

            if (!AddressUtility.CheckUrlReservation(port))
            {
                Log.Information("Reserving port {Port} for websocket...", port);

                if (!AddressUtility.AddUrlReservation(port))
                    Log.Error("Failed to reserve port. Websocket connection may fail.");
            }

            port = openPort;
        }

        return true;
    }

    private bool TryValidateClientWebSocketDestination(out string address, out int port)
    {
        port    = 0;
        address = string.Empty;

        if (string.IsNullOrEmpty(ipTextbox.Text))
        {
            Log.Error("IP Address cannot be null.");
            return false;
        }

        if (string.IsNullOrEmpty(portTextbox.Text))
        {
            Log.Error("Port cannot be null.");
            return false;
        }

        if (!int.TryParse(portTextbox.Text, out port) || port is <= 0 or >= ushort.MaxValue)
        {
            Log.Error("Port is invalid.");
            return false;
        }

        address = ipTextbox.Text;
        return true;
    }

    private async Task DisconnectServices()
    {
        connectButton.Text = "Disconnecting...";

        await _servicesMarshal.StopAsync()
                              .ContinueWith(_ => CompleteDisconnection(), Program.ApplicationExitCancellationToken,
                                            TaskContinuationOptions.OnlyOnRanToCompletion,
                                            TaskScheduler.FromCurrentSynchronizationContext());
        return;

        void CompleteDisconnection()
        {
            connectButton.Text    = _connectButtonText;
            _hasConnection        = false;
            connectButton.Enabled = true;
        }
    }

    private void isClient_CheckedChanged(object sender, EventArgs e)
    {
        if (!isClient.Checked)
            return;

        ipTextbox.ReadOnly = false;
        connectButton.Text = _connectButtonText = "Connect";
    }

    private void isHost_CheckedChanged(object sender, EventArgs e)
    {
        if (!isHost.Checked)
            return;

        ipTextbox.ReadOnly = true;
        connectButton.Text = _connectButtonText = "Host";
    }

    private void AddressPasted(object sender, ClipboardEventArgs e)
    {
        var args = e.ClipboardText.Split(':');
        if (args.Length != 2)
        {
            ((PastableTextBox)sender).Text = e.ClipboardText;
            return;
        }

        ipTextbox.Text   = args[0];
        portTextbox.Text = args[1];
    }

    private void OnIPTextChanged(object sender, EventArgs e)
    {
        if (ipTextbox.Text[^1] != ':')
            return;

        ipTextbox.Text = ipTextbox.Text[..^1];
        portTextbox.Focus();
    }
}
