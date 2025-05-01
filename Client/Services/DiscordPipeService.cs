using System.Net.Http.Json;
using Client.Models;
using Dec.DiscordIPC;
using Dec.DiscordIPC.Commands;
using Dec.DiscordIPC.Entities;
using Dec.DiscordIPC.Events;
using Serilog;

namespace Client.Services;

public sealed class DiscordPipeService : IService
{
    private const string DiscordOAuthClientIdField     = "DiscordOAuthClientId";
    private const string DiscordOAuthClientSecretField = "DiscordOAuthClientSecret";
    private const string DiscordOAuthRedirectField     = "DiscordOAuthRedirectUri";

    public  User?                      LocalUser      { get; set; }
    public  Dictionary<string, double> DefaultVolumes { get; set; } = new();
    private DiscordIPC?                DiscordPipe    { get; set; }
    private HttpClient?                HttpClient     { get; set; }

    private string? CurrentVoiceChannel { get; set; }

    private readonly HttpClientHandler _httpRequestHandler = new()
    {
        UseProxy = true
    };

    public DiscordPipeService()
    {
        HttpClient             = new HttpClient(_httpRequestHandler);
        HttpClient.BaseAddress = new Uri("https://discord.com/");
    }

    public async Task<bool> StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (DiscordPipe == null)
        {
            if (string.IsNullOrEmpty(Program.GetConfig(DiscordOAuthClientIdField))     ||
                string.IsNullOrEmpty(Program.GetConfig(DiscordOAuthClientSecretField)) ||
                string.IsNullOrEmpty(Program.GetConfig(DiscordOAuthRedirectField)))
            {
                Log.Error("Missing Discord OAuth Client ID or Secret.");
                return false;
            }

            Log.Information("Connecting to Discord...");
            DiscordPipe = new DiscordIPC(Program.GetConfig(DiscordOAuthClientIdField));
            await DiscordPipe.InitAsync();

            string accessToken;
            try
            {
                var codeResponse = await DiscordPipe.SendCommandAsync(
                    new Authorize.Args
                    {
                        scopes    = ["rpc", "rpc.voice.read", "rpc.voice.write"],
                        client_id = Program.GetConfig(DiscordOAuthClientIdField)
                    });

                var oauth2 = await GetOAuth2Token(codeResponse.code);
                if (oauth2 == null)
                {
                    Log.Error("Failed to get OAuth2 token. Make sure you have a stable internet connection.");
                    return false;
                }

                accessToken = oauth2.access_token;
            }
            catch (ErrorResponseException)
            {
                Log.Error("Authorization denied.");
                return false;
            }

            try
            {
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

                Log.Information("Connected to Discord as {Username}.", LocalUser.username);
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Error initializing Discord IPC: {Message}", e.Message);
                return false;
            }
        }

        return true;
    }

    private void SetVoiceMembers(List<VoiceStateCreate.Data> voiceStates)
    {
        foreach (var voice in voiceStates)
        {
            if (voice.user.id == LocalUser!.id)
                continue;

            DefaultVolumes[voice.user.id] = voice.volume.GetValueOrDefault(100);

            if (Main.useVerboseLogging)
                Log.Information("Tracking {Username} from Discord with default volume {Volume}.",
                                voice.user.username, voice.volume.GetValueOrDefault(100));
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

                Log.Information($"Left Discord voice channel.");
                foreach (var (discordId, volume) in DefaultVolumes)
                    await SetUserVolume(discordId, volume);
                DefaultVolumes.Clear();
                DiscordPipe!.OnVoiceStateCreate -= OnVoiceStateCreate;
                await DiscordPipe.UnsubscribeAsync(new VoiceStateCreate.Args { channel_id = CurrentVoiceChannel });
                CurrentVoiceChannel = null;
                return;
            }

            CurrentVoiceChannel = data.channel_id;
            var voiceChannelInfo = await DiscordPipe!.SendCommandAsync(new GetSelectedVoiceChannel.Args());
            SetVoiceMembers(voiceChannelInfo.voice_states);

            DiscordPipe!.OnVoiceStateCreate += OnVoiceStateCreate;
            await DiscordPipe.SubscribeAsync(new VoiceStateCreate.Args
            {
                channel_id = CurrentVoiceChannel
            });
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while join/leaving voice channel: {Message}", e.Message);
        }
    }

    private void OnVoiceStateCreate(object? sender, VoiceStateCreate.Data e)
    {
        if (e.user.id == LocalUser!.id)
            return;

        Log.Information("Tracking {UserUsername} from Discord.", e.user.username);

        // TODO)) Temporary fix
        var volume = e.volume.GetValueOrDefault(100);
        DefaultVolumes[e.user.id] = volume < 80 ? 80 : volume;
    }

    private async Task<HttpAuthenticationContent?> GetOAuth2Token(string code)
    {
        if (HttpClient == null)
            return null;

        var records = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("client_id", Program.GetConfig(DiscordOAuthClientIdField)),
            new("client_secret", Program.GetConfig(DiscordOAuthClientSecretField)),
            new("code", code),
            new("redirect_uri", Program.GetConfig(DiscordOAuthRedirectField))
        };

        using var authenticateRequest = new HttpRequestMessage(HttpMethod.Post, "api/oauth2/token");
        authenticateRequest.Content = new FormUrlEncodedContent(records);
        using var authentication = await HttpClient.SendAsync(authenticateRequest);

        if (!authentication.IsSuccessStatusCode)
            return null;

        var httpAuthentication = await authentication.Content.ReadFromJsonAsync<HttpAuthenticationContent>();
        return httpAuthentication;
    }

    /// <param name="discordId">Discord user ID</param>
    /// <param name="volume">A perceptual volume between 0 and 1</param>
    public async Task SetUserVolume(string discordId, double volume)
    {
        try
        {
            if (DiscordPipe == null)
                return;

            if (!DefaultVolumes.TryGetValue(discordId, out var defaultVolume))
                return;

            var args = new SetUserVoiceSettings.Args
            {
                user_id = discordId,
                volume  = defaultVolume * volume
            };

            // If this doesn't work, change L64 on LowLevelDiscordIPC.cs to use nonce from new parameter
            if (Main.useVerboseLogging)
                Log.Information("Set {DiscordId} volume to {Volume}.", discordId, args.volume);
            await DiscordPipe.SendCommandAsync(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while setting voice settings: {Message}", e.Message);
        }
    }

    public async Task ResetUserVolume(string discordId)
    {
        try
        {
            if (DiscordPipe == null)
                return;

            if (!DefaultVolumes.TryGetValue(discordId, out var defaultVolume))
                return;

            var args = new SetUserVoiceSettings.Args
            {
                user_id = discordId,
                volume  = defaultVolume
            };

            Log.Information("Reset {DiscordId} volume to {Volume}.", discordId, args.volume);
            await DiscordPipe.SendCommandAsync(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while resetting voice settings: {Message}", e.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DefaultVolumes.Clear();
        DiscordPipe?.Dispose();
        DiscordPipe = null;

        Log.Information("Terminated Discord IPC.");
        return Task.CompletedTask;
    }
}
