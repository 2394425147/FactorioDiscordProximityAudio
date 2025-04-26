namespace Client.Models;

public sealed class ClientPosition
{
    public string DiscordId    { get; set; } = string.Empty;
    public double X            { get; set; }
    public double Y            { get; set; }
    public int    SurfaceIndex { get; set; }
}
