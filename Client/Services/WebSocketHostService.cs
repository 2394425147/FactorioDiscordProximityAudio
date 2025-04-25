using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;


namespace Client.Services;

public sealed class WebSocketHostService(int port) : IReportingService
{
    public const int StartingPort   = 8970;
    public const int CheckPortCount = 1029;

    public bool Started { get; private set; }

    private HttpListener? HttpListener { get; set; }

    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        progress.Report(new LogItem("Raising privileges for port access...", LogItem.LogType.Info));

        // Execute netsh http add urlacl url=http://+:port/ user=DOMAIN\user as admin
        var commandInfo = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = $"/k netsh http add urlacl url=http://+:{port}/ user=\"{Environment.UserDomainName}\\{Environment.UserName}\"",
            UseShellExecute = true,
            // CreateNoWindow = true,
            // WindowStyle = /ProcessWindowStyle.Hidden,
            // Verb = "runas"
        };

        var process = Process.Start(commandInfo);
        process?.WaitForExit();

        progress.Report(new LogItem($"Opening websocket...", LogItem.LogType.Info));

        HttpListener = new HttpListener();
        HttpListener.Prefixes.Add($"http://+:{port}/");
        HttpListener.Start();

        ListenerMainLoop(progress, cancellationToken).ConfigureAwait(false);

        Started = true;
        progress.Report(new LogItem($"Listening for websocket connections at *:{port}...", LogItem.LogType.Info));
        return Task.CompletedTask;
    }

    private async Task ListenerMainLoop(IProgress<LogItem> progress, CancellationToken cancellationToken = default)
    {
        while (HttpListener?.IsListening ?? false)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var context = await HttpListener.GetContextAsync();

            context.Response.AppendHeader("Access-Control-Allow-Origin", "*");

            if (context.Request.IsWebSocketRequest)
            {
                var socket     = await context.AcceptWebSocketAsync(subProtocol: null);
                var socketId   = Guid.NewGuid();
                var socketTask = HandleSocketAsync(progress, socket, socketId);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }

            await Task.Yield();
        }
    }

    private async Task HandleSocketAsync(IProgress<LogItem> progress, HttpListenerWebSocketContext socket, Guid socketId)
    {
        while (socket.WebSocket.State == WebSocketState.Open)
        {
            var buffer   = new ArraySegment<byte>(new byte[4096]);
            var received = await socket.WebSocket.ReceiveAsync(buffer, CancellationToken.None);

            if (received.MessageType == WebSocketMessageType.Close)
            {
                await socket.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            else
            {
                var message = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
                progress.Report(new LogItem($"Received message: {message}", LogItem.LogType.Info));

                var response = Encoding.UTF8.GetBytes("Hello, " + message + "!");
                buffer = new ArraySegment<byte>(response);
                await socket.WebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        HttpListener?.Stop();
        Started = false;
        return Task.CompletedTask;
    }
}
