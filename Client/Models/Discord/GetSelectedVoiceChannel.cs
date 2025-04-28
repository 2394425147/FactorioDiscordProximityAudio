// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

namespace Client.Models.Discord;

public sealed class GetSelectedVoiceChannelSend : Payload
{
    public GetSelectedVoiceChannelSend(string nonce)
    {
        this.nonce = nonce;
    }

    public override string cmd { get; set; } = "GET_SELECTED_VOICE_CHANNEL";
}

public sealed class GetSelectedVoiceChannelReceive : ReceivePayload<GetSelectedVoiceChannelReceive.Data>
{
    public class Data
    {
        public string        id           { get; set; }
        public string        name         { get; set; }
        public int           type         { get; set; }
        public string        topic        { get; set; }
        public int           bitrate      { get; set; }
        public int           user_limit   { get; set; }
        public string        guild_id     { get; set; }
        public int           position     { get; set; }
        public object[]      messages     { get; set; }
        public VoiceStates[] voice_states { get; set; }
    }
}
