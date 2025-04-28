namespace Client.Models.Discord;

public sealed class SetUserVoiceSettingsSend : SendPayload<SetUserVoiceSettingsSend.Args>
{
    public override string cmd { get; set; } = "SET_USER_VOICE_SETTINGS";

    public SetUserVoiceSettingsSend(string nonce, string userId, Pan pan)
    {
        this.nonce = nonce;
        args       = new Args { user_id = userId, pan = pan };
    }

    public class Args
    {
        public string user_id { get; set; }
        public Pan    pan     { get; set; }
    }
}
