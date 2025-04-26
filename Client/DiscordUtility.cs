namespace Client;

public static class DiscordUtility
{
    public const  int    MaxUidLength = 18;
    public static string GetFixedLengthUid(string uid) => uid.PadLeft(18, ' ');

    public static string GetUid(string fixedLengthUid)
    {
        if (fixedLengthUid[0] == ' ')
            fixedLengthUid = fixedLengthUid.Substring(1, 17);
        return fixedLengthUid;
    }
}
