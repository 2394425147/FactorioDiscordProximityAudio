using System.Security.Cryptography;

namespace Client;

public static class DiscordUtility
{
    public const int MaxUidLength = 18;

    public static string GetFixedLengthUid(string discordId) =>
        discordId.PadRight(MaxUidLength, '\0');

    public static string GetUid(string fixedLengthUid) =>
        fixedLengthUid.TrimEnd('\0');

    public static Guid CreateCryptographicallySecureGuid()
    {
        using var provider = RandomNumberGenerator.Create();
        var       bytes    = new byte[16];
        provider.GetBytes(bytes);

        return new Guid(bytes);
    }

    public static string[] GetNamedPipes() => Directory.GetFiles(@"\\.\pipe\", "discord-ipc-*");
}
