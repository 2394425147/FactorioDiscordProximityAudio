using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Client.Models.DiscordPacket;

namespace Client.Services;

public sealed class DiscordNamedPipeService : IReportingService
{
    ***REMOVED***
    public        bool   Started { get; private set; }

    private NamedPipeClientStream Pipe { get; set; } = null!;

    public async Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Started = false;
        await SendHandshake(progress, cancellationToken);
    }

    public async Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        if (Pipe.IsConnected)
        {
            progress.Report(new LogItem("Closing connection to Discord...", LogItem.LogType.Info));

            var packet = CreatePacket(PacketOpCode.Close, "{}");
            await Pipe.WriteAsync(packet, cancellationToken);

            ReceivePacket(Pipe, out var responseOp, out _);
            progress.Report(new LogItem($"Received response (OP: {responseOp})", LogItem.LogType.Info));
        }

        Started = false;
        await Pipe.DisposeAsync();
    }

    public async Task SendHandshake(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        try
        {
            var openNamedPipe = AddressUtility.GetDiscordNamedPipes().FirstOrDefault();

            if (string.IsNullOrEmpty(openNamedPipe))
            {
                progress.Report(new LogItem("Named pipe is not available.", LogItem.LogType.Error));
                return;
            }

            openNamedPipe = openNamedPipe[9..];

            progress.Report(new LogItem($"Connecting to {openNamedPipe}...", LogItem.LogType.Info));

            Pipe = new NamedPipeClientStream(".", openNamedPipe, PipeDirection.InOut);
            await Pipe.ConnectAsync(3000, cancellationToken);

            if (!await TryHandshake(progress, cancellationToken))
                return;

            Started = true;
        }
        catch (Exception ex)
        {
            progress.Report(new LogItem(ex.Message, LogItem.LogType.Error));
        }
    }

    private async Task<bool> TryHandshake(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        try
        {
            progress.Report(new LogItem($"Sending handshake to Discord...", LogItem.LogType.Info));

            var payload = new { v = 1, client_id = ApplicationClientId };
            var json    = JsonSerializer.Serialize(payload);
            var packet  = CreatePacket(PacketOpCode.Handshake, json);
            await Pipe.WriteAsync(packet, cancellationToken);
            await Pipe.FlushAsync(cancellationToken);

            ReceivePacket(Pipe, out _, out var receivedJson);

            var receivedHandshake = JsonSerializer.Deserialize<Handshake.RootObject>(receivedJson);

            if (receivedHandshake == null)
                throw new Exception("Handshake failed.");

            progress.Report(new LogItem($"Connected to Discord: {receivedHandshake.data.user.username}", LogItem.LogType.Info));
            return true;
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
            return false;
        }
    }

    private static byte[] CreatePacket(PacketOpCode opCode, string json)
    {
        var       jsonBytes = Encoding.UTF8.GetBytes(json);
        using var ms        = new MemoryStream();
        using var writer    = new BinaryWriter(ms);
        writer.Write((int)opCode);
        writer.Write(jsonBytes.Length);
        writer.Write(jsonBytes);
        return ms.ToArray();
    }

    private static void ReceivePacket(NamedPipeClientStream pipe, out PacketOpCode opCode, out string json)
    {
        var header         = ReadBytes(pipe, 8);
        var responseOp     = BitConverter.ToInt32(header, 0);
        var responseLength = BitConverter.ToInt32(header, 4);

        var responseData = ReadBytes(pipe, responseLength);
        var responseJson = Encoding.UTF8.GetString(responseData);

        opCode = (PacketOpCode)responseOp;
        json   = responseJson;
    }

    private static byte[] ReadBytes(Stream stream, int count)
    {
        var buffer    = new byte[count];
        var bytesRead = 0;

        while (bytesRead < count)
        {
            var read = stream.Read(buffer, bytesRead, count - bytesRead);
            if (read == 0) throw new EndOfStreamException();
            bytesRead += read;
        }

        return buffer;
    }

    public enum PacketOpCode
    {
        Handshake = 0,
        Frame     = 1,
        Close     = 2,
        Ping      = 3,
        Pong      = 4,
    }
}
