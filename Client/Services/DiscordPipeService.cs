using System.Net.Http.Json;
using Client.Models;
using Dec.DiscordIPC;
using Dec.DiscordIPC.Commands;
using Dec.DiscordIPC.Entities;
using Dec.DiscordIPC.Events;

namespace Client.Services;

public sealed class DiscordPipeService : IReportingService
{
    // TODO)) Replace with .env
    ***REMOVED***
    ***REMOVED***

    public  bool                                 Started     { get; private set; }
    public  User?                                LocalUser   { get; set; }
    public  Dictionary<string, DiscordVoiceUser> VoiceUsers  { get; set; } = new();
    private DiscordIPC?                          DiscordPipe { get; set; }
    private HttpClient?                          HttpClient  { get; set; }

    private string? CurrentVoiceChannel { get; set; }

    private readonly HttpClientHandler _httpRequestHandler = new()
    {
        UseProxy = true
    };

    private IProgress<LogItem>? Progress { get; set; }

    public DiscordPipeService()
    {
        HttpClient             = new HttpClient(_httpRequestHandler);
        HttpClient.BaseAddress = new Uri("https://discord.com/");
    }

    public async Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Progress = progress;
        Started  = false;

        if (DiscordPipe == null)
        {
            DiscordPipe = new DiscordIPC(ApplicationClientId);
            await DiscordPipe.InitAsync();

            string accessToken;
            try
            {
                var codeResponse = await DiscordPipe.SendCommandAsync(
                    new Authorize.Args
                    {
                        scopes    = ["rpc", "rpc.voice.read", "rpc.voice.write"],
                        client_id = ApplicationClientId
                    });

                var oauth2 = await GetOAuth2Token(codeResponse.code);
                if (oauth2 == null)
                {
                    Progress!.Report(new LogItem("Failed to get OAuth2 token.", LogItem.LogType.Error));
                    return;
                }

                accessToken = oauth2.access_token;
            }
            catch (ErrorResponseException)
            {
                progress.Report(new LogItem("Authorization denied.", LogItem.LogType.Error));
                return;
            }

            var authData = await DiscordPipe.SendCommandAsync(new Authenticate.Args
            {
                access_token = accessToken,
            });

            LocalUser = authData.user;

            var selectedVoiceChannel = await DiscordPipe.SendCommandAsync(new GetSelectedVoiceChannel.Args());

            if (selectedVoiceChannel != null && !string.IsNullOrEmpty(selectedVoiceChannel.id))
            {
                CurrentVoiceChannel = selectedVoiceChannel.id;
                SetVoiceMembers(selectedVoiceChannel.voice_states);
            }

            DiscordPipe.OnVoiceChannelSelect += OnVoiceChannelSelect;
            await DiscordPipe.SubscribeAsync(new VoiceChannelSelect.Args());
        }

        Started = true;
    }

    private void SetVoiceMembers(List<VoiceStateCreate.Data> voiceStates)
    {
        foreach (var voice in voiceStates)
        {
            if (voice.user.id == LocalUser!.id)
                continue;

            Progress?.Report(new LogItem($"User {voice.user.id} added.", LogItem.LogType.Info));
            VoiceUsers[voice.user.id] =
                new DiscordVoiceUser(voice.user.id, voice.pan.left.GetValueOrDefault(1), voice.pan.right.GetValueOrDefault(1));
        }
    }

    public async void OnVoiceChannelSelect(object? sender, VoiceChannelSelect.Data data)
    {
        try
        {
            if (string.IsNullOrEmpty(data.channel_id))
            {
                if (CurrentVoiceChannel == null)
                    return;

                Progress?.Report(new LogItem($"Left Discord voice channel.", LogItem.LogType.Info));
                DiscordPipe!.OnVoiceStateCreate -= OnVoiceStateCreate;
                await DiscordPipe.UnsubscribeAsync(new VoiceStateCreate.Args
                {
                    channel_id = CurrentVoiceChannel
                });
                CurrentVoiceChannel = null;
                return;
            }

            CurrentVoiceChannel = data.channel_id;

            DiscordPipe!.OnVoiceStateCreate += OnVoiceStateCreate;
            await DiscordPipe.SubscribeAsync(new VoiceStateCreate.Args
            {
                channel_id = CurrentVoiceChannel
            });
        }
        catch (Exception e)
        {
            Progress!.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }

    private void OnVoiceStateCreate(object? sender, VoiceStateCreate.Data e)
    {
        if (e.user.id == LocalUser!.id)
            return;

        Progress?.Report(new LogItem($"User {e.user.id} added.", LogItem.LogType.Info));
        VoiceUsers[e.user.id] =
            new DiscordVoiceUser(e.user.id, e.pan.left.GetValueOrDefault(1), e.pan.right.GetValueOrDefault(1));
    }

    private async Task<HttpAuthenticationContent?> GetOAuth2Token(string code)
    {
        if (HttpClient == null)
            return null;

        var records = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("client_id", ApplicationClientId),
            new("client_secret", ApplicationClientSecret),
            new("code", code),
            new("redirect_uri", "http://localhost/test")
        };

        using var authenticateRequest = new HttpRequestMessage(HttpMethod.Post, "api/oauth2/token");
        authenticateRequest.Content = new FormUrlEncodedContent(records);
        using var authentication = await HttpClient.SendAsync(authenticateRequest);

        if (!authentication.IsSuccessStatusCode)
            return null;

        var httpAuthentication = await authentication.Content.ReadFromJsonAsync<HttpAuthenticationContent>();
        return httpAuthentication;
    }

    public async Task SetUserVoiceSettings(string discordId, SetUserVoiceSettings.Pan pan)
    {
        if (DiscordPipe == null)
            return;

        var args = new SetUserVoiceSettings.Args
        {
            user_id = discordId,
            pan     = pan
        };

        await DiscordPipe.SendCommandAsync(args);
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        if (DiscordPipe != null)
        {
            progress.Report(new LogItem("Closing connection to Discord...", LogItem.LogType.Info));
            DiscordPipe.Dispose();
        }

        DiscordPipe = null;
        Started     = false;
        return Task.CompletedTask;
    }
}
