namespace Client.Models.Discord;

public class VoiceStates
{
    public string     nick        { get; set; }
    public bool       mute        { get; set; }
    public float      volume      { get; set; }
    public Pan        pan         { get; set; }
    public VoiceState voice_state { get; set; }
    public User       user        { get; set; }
}
