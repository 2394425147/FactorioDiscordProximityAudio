using System.Buffers;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Client.Models;
using Client.Models.Discord;
using Client.Models.Discord.Subscriptions;

namespace Client.Services;

public sealed class DiscordNamedPipeService : IReportingService
{
    // TODO)) Replace with .env
    ***REMOVED***
    ***REMOVED***

    public bool                                 Started         { get; private set; }
    public Handshake?                           HandshakePacket { get; set; }
    public Dictionary<string, DiscordVoiceUser> VoiceUsers      { get; set; } = new();

    private CancellationTokenSource? CancelConnectionSource { get; set; }
    private NamedPipeClientStream    Pipe                   { get; set; } = null!;
    private HttpClient?              HttpClient             { get; set; }

    private string Nonce { get; set; } = string.Empty;

    private string? CurrentVoiceChannel { get; set; }

    private readonly HttpClientHandler _httpRequestHandler = new()
    {
        UseProxy = true
    };

    public async Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Started = false;

        if (HttpClient == null)
        {
            HttpClient             = new HttpClient(_httpRequestHandler);
            HttpClient.BaseAddress = new Uri("https://discord.com/");
        }

        Nonce                  = DiscordUtility.CreateCryptographicallySecureGuid().ToString().ToLower();
        CancelConnectionSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await SendHandshake(progress, CancelConnectionSource.Token);
        _       = Task.Run(() => MainLoop(progress, CancelConnectionSource.Token), CancelConnectionSource.Token);
        Started = true;
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

            Pipe = new NamedPipeClientStream(".", openNamedPipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            await Pipe.ConnectAsync(3000, cancellationToken);

            if (!await TryAuthorize(progress, cancellationToken))
            {
                progress.Report(new LogItem("Discord authentication failed.", LogItem.LogType.Error));
            }
        }
        catch (Exception ex)
        {
            progress.Report(new LogItem(ex.Message, LogItem.LogType.Error, ex.ToString()));
        }
    }

    private async Task<bool> TryAuthorize(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        try
        {
            progress.Report(new LogItem($"Requesting Discord access...", LogItem.LogType.Info));

            if (!await EstablishHandshake(progress, cancellationToken))
                return false;

            await new AuthorizeSend(ApplicationClientId, ["rpc", "rpc.voice.read", "rpc.voice.write"], Nonce)
                .Send(Pipe, cancellationToken);

            var (authorizeOpCode, authorization) = await ReadPipe<AuthorizeReceive>(progress, cancellationToken);

            if (authorizeOpCode == Payload.OpCode.Error)
            {
                progress.Report(new LogItem("Discord authorization failed [X].", LogItem.LogType.Error));
                return false;
            }

            var oauth2 = await GetOAuth2Token(authorization!.data!.code, cancellationToken);

            if (oauth2 == null)
                return false;

            await new AuthenticateSend(oauth2.access_token, Nonce).Send(Pipe, cancellationToken);

            _ = ReadPipe<AuthenticateReceive>(progress, cancellationToken);

            await new GetSelectedVoiceChannelSend(Nonce).Send(Pipe, cancellationToken);

            var (voiceChannelOpCode, voiceChannelInfo) =
                await ReadPipe<GetSelectedVoiceChannelReceive>(progress, cancellationToken);

            if (voiceChannelOpCode == Payload.OpCode.Error)
            {
                progress.Report(new LogItem("Discord authorization failed [2].", LogItem.LogType.Error));
                return false;
            }

            if (voiceChannelInfo is { data: not null })
            {
                foreach (var voice in voiceChannelInfo.data.voice_states)
                {
                    if (voice.user.id == HandshakePacket!.data!.user.id)
                        continue;

                    VoiceUsers[voice.user.id] = new DiscordVoiceUser(voice.user.id, voice.pan.left, voice.pan.right);
                }
            }

            await new LocalVoiceChannelChangeSend(Nonce).Send(Pipe, cancellationToken);
            _ = await ReadPipe(cancellationToken);

            progress.Report(
                new LogItem($"Connected to Discord as {HandshakePacket!.data!.user.username}.", LogItem.LogType.Info));

            return true;
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
            return false;
        }
    }

    public async Task MainLoop(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        try
        {
            while (Pipe.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var (opCode, jsonString) = await ReadPipe(cancellationToken);
                progress.Report(new LogItem($"{opCode}: {jsonString}", LogItem.LogType.Info));
                var jsonDocument = JsonDocument.Parse(jsonString);

                switch (opCode)
                {
                    case Payload.OpCode.Frame:
                        var evt = jsonDocument.RootElement.GetProperty("evt").GetString();
                        await Task.Run(
                            () => ProcessEvent(progress, evt, jsonDocument.RootElement.GetProperty("data").GetRawText(),
                                               cancellationToken), cancellationToken);
                        break;
                    case Payload.OpCode.Ping:
                        var packet = Payload.Pack(Payload.OpCode.Pong, jsonString);
                        await Pipe.WriteAsync(packet, cancellationToken);
                        await Pipe.FlushAsync(cancellationToken);
                        break;
                    case Payload.OpCode.Pong:
                        break;
                    case Payload.OpCode.Handshake:
                    case Payload.OpCode.Close:
                        return;
                    case Payload.OpCode.Error:
                        progress.Report(new LogItem(jsonString, LogItem.LogType.Error));
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        catch (TaskCanceledException)
        {
            // ignored
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }

    private async Task ProcessEvent(IProgress<LogItem> progress, string? evt, string? data, CancellationToken cancellationToken)
    {
        try
        {
            switch (evt)
            {
                case "VOICE_CHANNEL_SELECT":
                    if (data == null)
                        return;

                    var voiceChannel = JsonSerializer.Deserialize<LocalVoiceChannelChangeReceiveData>(data);

                    if (voiceChannel?.channel_id == null || voiceChannel.guild_id == null)
                    {
                        if (CurrentVoiceChannel == null)
                            return;

                        await new VoiceChannelAnyJoinedUnsubscribe(Nonce, CurrentVoiceChannel).Send(Pipe, cancellationToken);
                        _ = ReadPipe(cancellationToken);
                        progress.Report(
                            new LogItem(
                                $"Left Discord voice channel.",
                                LogItem.LogType.Info));

                        CurrentVoiceChannel = null;
                        return;
                    }

                    if (CurrentVoiceChannel == voiceChannel.channel_id)
                        return;

                    CurrentVoiceChannel = voiceChannel.channel_id;

                    await new GetSelectedVoiceChannelSend(Nonce).Send(Pipe, cancellationToken);

                    var (_, voiceChannelInfo) = await ReadPipe<GetSelectedVoiceChannelReceive>(progress, cancellationToken);

                    if (voiceChannelInfo == null)
                        return;

                    progress.Report(
                        new LogItem($"Joined Discord voice channel {voiceChannelInfo.data!.name}.", LogItem.LogType.Info));

                    foreach (var voice in voiceChannelInfo.data.voice_states)
                    {
                        if (voice.user.id == HandshakePacket!.data!.user.id)
                            continue;

                        progress.Report(new LogItem($"User {voice.user.id} added.", LogItem.LogType.Info));
                        VoiceUsers[voice.user.id] = new DiscordVoiceUser(voice.user.id, voice.pan.left, voice.pan.right);
                    }

                    await new VoiceChannelAnyJoinedSubscribe(Nonce, voiceChannel.channel_id).Send(Pipe, cancellationToken);
                    _ = ReadPipe(cancellationToken);
                    break;
            }
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }

    private async Task<bool> EstablishHandshake(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        var json   = JsonSerializer.Serialize(new { v = 1, client_id = ApplicationClientId });
        var packet = Payload.Pack(Payload.OpCode.Handshake, json);
        await Pipe.WriteAsync(packet, cancellationToken);
        await Pipe.FlushAsync(cancellationToken);

        var (opCode, handshake) = await ReadPipe<Handshake>(progress, cancellationToken);
        if (opCode == Payload.OpCode.Error)
            return false;

        HandshakePacket = handshake;
        return true;
    }

    private async Task<HttpAuthenticateReceive?> GetOAuth2Token(string code, CancellationToken cancellationToken)
    {
        if (HttpClient == null)
            return null;

        var httpAuthenticate = HttpAuthenticateSend.Create(ApplicationClientId, ApplicationClientSecret, code,
                                                           "http://localhost/test");

        using var authenticateRequest = new HttpRequestMessage(HttpMethod.Post, "api/oauth2/token");
        authenticateRequest.Content = httpAuthenticate;
        using var authentication = await HttpClient.SendAsync(authenticateRequest, cancellationToken);

        if (!authentication.IsSuccessStatusCode)
            return null;

        var httpAuthentication = await authentication.Content.ReadFromJsonAsync<HttpAuthenticateReceive>(cancellationToken);
        return httpAuthentication;
    }

    private async Task<(Payload.OpCode, T?)> ReadPipe<T>(IProgress<LogItem> progress, CancellationToken cancellationToken)
        where T : Payload
    {
        var (opCode, jsonString) = await ReadPipe(cancellationToken);

        var jsonDocument = JsonDocument.Parse(jsonString);
        if (!jsonDocument.RootElement.TryGetProperty("evt", out var evt) || evt.GetString() == "ERROR")
        {
            progress.Report(new LogItem(jsonString, LogItem.LogType.Error));
            return (Payload.OpCode.Error, null);
        }

        var result = JsonSerializer.Deserialize<T>(jsonString);

        if (result == null)
        {
            progress.Report(new LogItem("2!",       LogItem.LogType.Error));
            progress.Report(new LogItem(jsonString, LogItem.LogType.Error));
            return (Payload.OpCode.Error, null);
        }

        return (opCode, result);
    }

    private async Task<(Payload.OpCode, string)> ReadPipe(CancellationToken cancellationToken)
    {
        var opCode         = Payload.OpCode.Error;
        var responseLength = 0;
        await ReadBytes(Pipe, 8, buffer =>
        {
            opCode         = (Payload.OpCode)BitConverter.ToInt32(buffer[..4]);
            responseLength = BitConverter.ToInt32(buffer[4..8]);
        }, cancellationToken);

        if (responseLength == 0)
            return (opCode, string.Empty);

        var response = string.Empty;
        await ReadBytes(Pipe, responseLength, buffer => { response = Encoding.UTF8.GetString(buffer); }, cancellationToken);
        return (opCode, response);
    }

    private static async Task ReadBytes(Stream                     stream,
                                        int                        count,
                                        Action<ReadOnlySpan<byte>> onRead,
                                        CancellationToken          cancellationToken)
    {
        if (count >= 1024 * 1024)
            throw new InvalidOperationException("Too many bytes to read.");

        var buffer    = ArrayPool<byte>.Shared.Rent(count);
        var bytesRead = 0;

        while (bytesRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(bytesRead, count - bytesRead), cancellationToken);
            if (read == 0)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw new EndOfStreamException();
            }

            bytesRead += read;
        }

        onRead.Invoke(buffer.AsSpan(0, count));
        ArrayPool<byte>.Shared.Return(buffer);
    }

    public async Task SetUserVoiceSettings(IProgress<LogItem> progress, string discordId, Pan pan, CancellationToken token)
    {
        if (!Pipe.IsConnected)
            return;

        try
        {
            await (CancelConnectionSource?.CancelAsync() ?? Task.CompletedTask);
        }
        catch (Exception)
        {
        }

        if (token != CancellationToken.None)
            CancelConnectionSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        else
        {
            CancelConnectionSource =
                CancellationTokenSource.CreateLinkedTokenSource(Program.applicationExitCancellationToken?.Token ??
                                                                CancellationToken.None);
        }

        _ = Task.Run(() => MainLoop(progress, CancelConnectionSource?.Token ?? CancellationToken.None),
                     CancelConnectionSource?.Token ?? CancellationToken.None);

        await new SetUserVoiceSettingsSend(Nonce, discordId, pan).Send(Pipe, token);
        _ = ReadPipe(token);
    }

    public async Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        if (Pipe.IsConnected)
        {
            progress.Report(new LogItem("Closing connection to Discord...", LogItem.LogType.Info));

            HttpClient?.CancelPendingRequests();
            await CancelConnectionSource!.CancelAsync();

            var packet = Payload.Pack(Payload.OpCode.Close, "{}");
            await Pipe.WriteAsync(packet, cancellationToken);

            var (opCode, payload) = await ReadPipe(CancellationToken.None);

            progress.Report(opCode != Payload.OpCode.Close
                                ? new LogItem($"Unexpected response (OP: {opCode})", LogItem.LogType.Error, payload)
                                : new LogItem($"Discord connection closed.",         LogItem.LogType.Info));
        }

        Started = false;
        await Pipe.DisposeAsync();
    }
}
