namespace Client.Models.Discord.Subscriptions;

public sealed class LocalVoiceChannelChangeSend : SendPayload<LocalVoiceChannelChangeSend.Args>
{
    public LocalVoiceChannelChangeSend(string nonce, string? guildId = null, string? channelId = null)
    {
        this.nonce = nonce;
        args = new Args
        {
            guild_id   = guildId,
            channel_id = channelId
        };
    }

    public class Args
    {
        public string? guild_id   { get; set; }
        public string? channel_id { get; set; }
    }

    public override string cmd { get; set; } = "SUBSCRIBE";
    public override string evt { get; set; } = "VOICE_CHANNEL_SELECT";
}

public sealed class LocalVoiceChannelChangeReceiveData
{
    public string? channel_id { get; set; }
    public string? guild_id   { get; set; }
}
