namespace Client.Models;

public sealed class ClientPosition
{
    public string           DiscordId { get; set; } = string.Empty;
    public FactorioPosition Position  { get; set; }
}
