using System.Security.Cryptography;

namespace Client;

public static class DiscordUtility
{
    public const int MaxUidLength = 18;

    public static string GetFixedLengthUid(string uid) => uid.PadLeft(18, ' ');

    public static string GetUid(string fixedLengthUid)
    {
        if (fixedLengthUid[0] == ' ')
            fixedLengthUid = fixedLengthUid.Substring(1, 17);
        return fixedLengthUid;
    }

    public static Guid CreateCryptographicallySecureGuid()
    {
        using var provider = RandomNumberGenerator.Create();
        var       bytes    = new byte[16];
        provider.GetBytes(bytes);

        return new Guid(bytes);
    }

    public static string[] GetNamedPipes() => Directory.GetFiles(@"\\.\pipe\", "discord-ipc-*");
}
