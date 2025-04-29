namespace Client.Models;

public sealed class DiscordVoiceUser(string userId, float volume)
{
    public string UserId { get; set; } = userId;
    public float  Volume { get; set; } = volume;
}
