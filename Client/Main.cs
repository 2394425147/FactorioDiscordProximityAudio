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
        isClient.Checked     = true;
        _fileSystemLogWriter = File.CreateText("log.txt");
    }

    private string _connectButtonText = string.Empty;
    private bool   _hasConnection;

    private void connectButton_Click(object sender, EventArgs e)
    {
        var hadConnection = _hasConnection;
        _hasConnection = !_hasConnection;

        if (hadConnection)
            DisconnectServices();
        else
            Task.Run(ConnectServices);

        ipTextbox.Enabled = portTextbox.Enabled = isClient.Enabled = isHost.Enabled = !_hasConnection;
    }

    private async Task ConnectServices()
    {
        _hasConnection = true;

        await uiThreadControl.InvokeAsync(() =>
        {
            ClearInMemoryLog();
            connectButton.Text    = "Connecting...";
            connectButton.Enabled = false;
        });

        var port    = 0;
        var address = string.Empty;
        if (isHost.Checked)
            await uiThreadControl.InvokeAsync(() => { port = TryValidateHostPort(); });
        else
            await uiThreadControl.InvokeAsync(() => { TryValidateClientAddressAndPort(out address, out port); });

        if (port == 0)
        {
            await CancelConnection();
            return;
        }

        var logger = new Progress<LogItem>(logItem => uiThreadControl.Invoke(() => AppendToLog(logItem)));
        var tasks = Program.clients.Values.Select(c => c.StartClient(logger,
                                                                     Program.applicationExitCancellationToken?.Token ??
                                                                     CancellationToken.None)).ToList();

        await Task.WhenAll(tasks);

        if (!Program.clients.Values.All(c => c.Started))
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
        else
        {
            var clientService = new WebSocketClientService(address, port);
            Program.RegisterService(clientService);
            await clientService.StartClient(logger, Program.applicationExitCancellationToken?.Token ?? CancellationToken.None);
            if (!clientService.Started)
            {
                await CancelConnection();
                return;
            }
        }

        await uiThreadControl.InvokeAsync(() =>
        {
            connectButton.Text    = "Disconnect";
            connectButton.Enabled = true;
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

    private void TryValidateClientAddressAndPort(out string address, out int port)
    {
        port    = 0;
        address = string.Empty;

        if (string.IsNullOrEmpty(ipTextbox.Text))
        {
            AppendToLog(new LogItem("IP Address cannot be null.", LogItem.LogType.Error));
            return;
        }

        if (string.IsNullOrEmpty(portTextbox.Text))
        {
            AppendToLog(new LogItem("Port cannot be null.", LogItem.LogType.Error));
            return;
        }

        if (!ushort.TryParse(portTextbox.Text, out var uShortPort) || port is <= 0 or >= 10000)
        {
            port = uShortPort;
            AppendToLog(new LogItem("Port is invalid.", LogItem.LogType.Error));
            return;
        }

        address = ipTextbox.Text;
    }

    private int TryValidateHostPort()
    {
        var portsInUse = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Select(x => x.Port).ToHashSet();

        if (string.IsNullOrEmpty(portTextbox.Text) || !int.TryParse(portTextbox.Text, out var port) ||
            port is <= 0 or >= ushort.MaxValue               || portsInUse.Contains(port))
        {
            AppendToLog(new LogItem("Port is left empty or unavailable. Finding a port...", LogItem.LogType.Warning));

            var openPort =
                AddressUtility.FindFirstAvailablePort(portsInUse, WebSocketHostService.StartingPort,
                                                      WebSocketHostService.CheckPortCount);
            if (openPort == 0)
            {
                AppendToLog(new LogItem("Port is occupied.", LogItem.LogType.Error));
                return 0;
            }

            port = openPort;

            portTextbox.Text = port.ToString();
        }

        return port;
    }

    private void DisconnectServices()
    {
        connectButton.Text = "Disconnecting...";

        var logger = new Progress<LogItem>(AppendToLog);
        var tasks  = Program.clients.Values.Select(c => c.StopClient(logger, CancellationToken.None));
        Task.WhenAll(tasks);

        Program.UnregisterService<WebSocketClientService>();
        Program.UnregisterService<WebSocketHostService>();

        connectButton.Text    = _connectButtonText;
        _hasConnection        = false;
        connectButton.Enabled = true;
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

    public void AppendToLog(LogItem logItem)
    {
        while (logList.Items.Count >= MaxLogLines)
            logList.Items.RemoveAt(0);

        var visibleLineCount = logList.ClientSize.Height / logList.ItemHeight;
        var isAtBottom       = logList.Items.Count - logList.TopIndex <= visibleLineCount + 1;
        logList.Items.Add(logItem);
        _fileSystemLogWriter?.WriteLine($"[{logItem.time}] [{logItem.type}] {logItem.message}");

        if (isAtBottom)
            logList.TopIndex = logList.Items.Count - 1;
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
