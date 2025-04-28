namespace Client.Models.Discord;

public sealed class SubscribeSend : SendPayload<SubscribeSend.Args>
{
    public SubscribeSend(string guildId, string nonce)
    {
        args       = new Args { guild_id = guildId };
        this.nonce = nonce;
    }

    public class Args
    {
        public string guild_id { get; set; }
    }

    public override string cmd { get; set; } = "SUBSCRIBE";
}
