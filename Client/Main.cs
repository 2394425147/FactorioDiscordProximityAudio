using System.Net.NetworkInformation;
using Client.Services;

namespace Client;

public sealed partial class Main : Form
{
    private const int MaxLogLines = 512;

    private       StreamWriter? _fileSystemLogWriter;
    public static Form          uiThreadControl = null!;

    public Main()
    {
        InitializeComponent();
        uiThreadControl = this;
    }

    private void Main_Load(object sender, EventArgs e)
    {
        isClient.Checked = true;
        portTextbox.Text = WebSocketHostService.StartingPort.ToString();

        try
        {
            _fileSystemLogWriter = File.CreateText("log.txt");
        }
        catch (Exception ex)
        {
            AppendToLog(new LogItem($"Failed to connect to log file. Logging will be disabled.", LogItem.LogType.Error,
                                    ex.ToString()));
        }

        toolTip1.SetToolTip(ipTextbox, "IP Address of the host to connect to. Defaults to 127.0.0.1 for host mode.");
        toolTip1.SetToolTip(
            portTextbox, $"Port of the host to connect to. Defaults to {WebSocketHostService.StartingPort} for host mode.");

        // TODO)) When rewriting this as a CLI tool, allow host-only mode for remote servers.
        toolTip1.SetToolTip(isHost,   "Host to manage player positions. A client connection will also be made.");
        toolTip1.SetToolTip(isClient, "Connect to a host to send and receive player positions.");
    }

    private string _connectButtonText = string.Empty;
    private bool   _hasConnection;

    private void connectButton_Click(object sender, EventArgs e)
    {
        var hadConnection = _hasConnection;
        _hasConnection = !_hasConnection;

        if (hadConnection)
            Task.Run(DisconnectServices);
        else
            Task.Run(ConnectServices);

        ipTextbox.Enabled = portTextbox.Enabled = isClient.Enabled = isHost.Enabled = !_hasConnection;
    }

    private async Task ConnectServices()
    {
        _hasConnection = true;

        var port               = 0;
        var address            = string.Empty;
        var isDestinationValid = false;

        await uiThreadControl.InvokeAsync(() =>
        {
            isDestinationValid = isHost.Checked
                ? TryValidateHostWebSocketDestination(out address, out port)
                : TryValidateClientWebSocketDestination(out address, out port);

            if (!isDestinationValid)
                return;

            ipTextbox.Text   = address;
            portTextbox.Text = port.ToString();

            ClearInMemoryLog();
            connectButton.Text    = "Connecting...";
            connectButton.Enabled = false;
        });

        if (!isDestinationValid)
        {
            await CancelConnection();
            return;
        }

        var logger = new Progress<LogItem>(logItem =>
        {
            if (uiThreadControl.IsHandleCreated)
            {
                if (uiThreadControl.InvokeRequired)
                    uiThreadControl.Invoke(() => AppendToLog(logItem));
                else
                    AppendToLog(logItem);
            }
            else
            {
                AppendToLog(logItem, true);
            }
        });
        var tasks = Program.Services.Select(c => c.StartClient(logger,
                                                               Program.applicationExitCancellationToken?.Token ??
                                                               CancellationToken.None)).ToList();

        await Task.WhenAll(tasks);

        if (!Program.Services.All(c => c.Started))
        {
            await CancelConnection();
            return;
        }

        if (isHost.Checked)
        {
            var hostService = new WebSocketHostService(port);
            Program.RegisterService(hostService);
            await hostService.StartClient(logger, Program.applicationExitCancellationToken?.Token ?? CancellationToken.None);
            if (!hostService.Started)
            {
                await CancelConnection();
                return;
            }
        }

        var clientService = new WebSocketClientService(address, port);
        Program.RegisterService(clientService);
        await clientService.StartClient(logger, Program.applicationExitCancellationToken?.Token ?? CancellationToken.None);
        if (!clientService.Started)
        {
            await CancelConnection();
            return;
        }

        var playerTrackerService = new PlayerTrackerService();
        Program.RegisterService(playerTrackerService);
        await playerTrackerService.StartClient(
            logger, Program.applicationExitCancellationToken?.Token ?? CancellationToken.None);
        if (!playerTrackerService.Started)
        {
            await CancelConnection();
            return;
        }

        await uiThreadControl.InvokeAsync(() =>
        {
            connectButton.Text        = "Disconnect";
            connectButton.Enabled     = true;
            playerDataGrid.DataSource = Program.GetService<PlayerTrackerService>()?.BindableClientPositions;
        });

        return;

        async Task CancelConnection()
        {
            await uiThreadControl.InvokeAsync(() =>
            {
                connectButton.Text    = _connectButtonText;
                connectButton.Enabled = true;
                _hasConnection        = false;
                ipTextbox.Enabled     = portTextbox.Enabled = isClient.Enabled = isHost.Enabled = !_hasConnection;
            });
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
            AppendToLog(new LogItem("Port is left empty or unavailable. Finding a port...", LogItem.LogType.Warning));

            var openPort =
                AddressUtility.FindFirstAvailablePort(portsInUse, WebSocketHostService.StartingPort,
                                                      WebSocketHostService.CheckPortCount);
            if (openPort == 0)
            {
                AppendToLog(new LogItem("Port is occupied.", LogItem.LogType.Error));
                return false;
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
            AppendToLog(new LogItem("IP Address cannot be null.", LogItem.LogType.Error));
            return false;
        }

        if (string.IsNullOrEmpty(portTextbox.Text))
        {
            AppendToLog(new LogItem("Port cannot be null.", LogItem.LogType.Error));
            return false;
        }

        if (!int.TryParse(portTextbox.Text, out port) || port is <= 0 or >= ushort.MaxValue)
        {
            AppendToLog(new LogItem("Port is invalid.", LogItem.LogType.Error));
            return false;
        }

        address = ipTextbox.Text;
        return true;
    }

    private async Task DisconnectServices()
    {
        await uiThreadControl.InvokeAsync(() => { connectButton.Text = "Disconnecting..."; });

        var logger = new Progress<LogItem>(logItem =>
        {
            if (uiThreadControl.IsHandleCreated)
            {
                if (uiThreadControl.InvokeRequired)
                    uiThreadControl.Invoke(() => AppendToLog(logItem));
                else
                    AppendToLog(logItem);
            }
            else
            {
                AppendToLog(logItem, true);
            }
        });

        var tasks = new List<Task>(Program.Services.Count);
        for (var i = Program.Services.Count - 1; i >= 0; i--)
        {
            var service = Program.Services[i];
            tasks.Add(service.StopClient(logger, Program.applicationExitCancellationToken?.Token ?? CancellationToken.None));
        }

        await Task.WhenAll(tasks);

        Program.UnregisterService<WebSocketClientService>();
        Program.UnregisterService<WebSocketHostService>();
        Program.UnregisterService<PlayerTrackerService>();

        await uiThreadControl.InvokeAsync(() =>
        {
            connectButton.Text        = _connectButtonText;
            _hasConnection            = false;
            connectButton.Enabled     = true;
            playerDataGrid.DataSource = null;
        });
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

    private void ClearInMemoryLog()
    {
        logList.Items.Clear();
    }

    public void AppendToLog(LogItem logItem, bool writeToFileOnly = false)
    {
        while (logList.Items.Count >= MaxLogLines)
            logList.Items.RemoveAt(0);

        if (!writeToFileOnly)
        {
            var visibleLineCount = logList.ClientSize.Height / logList.ItemHeight;
            var isAtBottom       = logList.Items.Count - logList.TopIndex <= visibleLineCount + 1;
            logList.Items.Add(logItem);
            _fileSystemLogWriter?.WriteLine($"[{logItem.time}] [{logItem.type}] {logItem.message}");
            if (isAtBottom) logList.TopIndex = logList.Items.Count - 1;
        }

        if (logItem.details != null)
            _fileSystemLogWriter?.WriteLine(logItem.details);

        _fileSystemLogWriter?.Flush();
    }

    private void Main_FormClosing(object sender, FormClosingEventArgs e)
    {
        _fileSystemLogWriter?.Close();
        _fileSystemLogWriter?.Dispose();
    }

    private void logList_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index == -1)
            return;

        var item = (LogItem)logList.Items[e.Index];

        e.Graphics.FillRectangle(Brushes.Black, e.Bounds);
        e.DrawFocusRectangle();

        var timeString = item.time.ToString("hh:mm:ss");
        var y          = (e.Index - logList.TopIndex) * logList.ItemHeight;
        var size       = TextRenderer.MeasureText(e.Graphics, timeString, logList.Font);

        TextRenderer.DrawText(e.Graphics, timeString, logList.Font, new Point(4, y), Color.Gray);
        TextRenderer.DrawText(e.Graphics, item.message, logList.Font, new Point(size.Width + 8, y),
                              LogItem.GetColor(item.type));
    }
}
