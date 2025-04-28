namespace Client.Models.Discord.Subscriptions;

public sealed class VoiceChannelAnyJoinedSubscribe : SendPayload<VoiceChannelAnyJoinedSubscribe.Args>
{
    public VoiceChannelAnyJoinedSubscribe(string nonce, string channelId)
    {
        this.nonce = nonce;
        args = new Args
        {
            channel_id = channelId
        };
    }

    public class Args
    {
        public string channel_id { get; set; }
    }

    public override string cmd { get; set; } = "SUBSCRIBE";
    public override string evt { get; set; } = "VOICE_CHANNEL_SELECT";
}

public sealed class VoiceChannelAnyJoinedUnsubscribe : SendPayload<VoiceChannelAnyJoinedUnsubscribe.Args>
{
    public VoiceChannelAnyJoinedUnsubscribe(string nonce, string channelId)
    {
        this.nonce = nonce;
        args = new Args
        {
            channel_id = channelId
        };
    }

    public class Args
    {
        public string channel_id { get; set; }
    }

    public override string cmd { get; set; } = "UNSUBSCRIBE";
    public override string evt { get; set; } = "VOICE_CHANNEL_SELECT";
}

public sealed class VoiceChannelAnyJoinedReceiveData
{
    public VoiceState voice_state { get; set; }
    public User       user        { get; set; }
    public string     nick        { get; set; }
    public int        volume      { get; set; }
    public bool       mute        { get; set; }
    public Pan        pan         { get; set; }
}
