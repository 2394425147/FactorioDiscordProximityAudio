namespace Client.Models;

public sealed class DiscordVoiceUser(string userId, float leftEar, float rightEar)
{
    public string UserId   { get; set; } = userId;
    public float  LeftEar  { get; set; } = leftEar;
    public float  RightEar { get; set; } = rightEar;
}
