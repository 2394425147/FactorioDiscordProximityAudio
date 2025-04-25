using System.Net.NetworkInformation;

namespace Client
{
    internal static class AddressUtility
    {
        /// <summary>
        /// https://stackoverflow.com/questions/5879605/udp-port-open-check
        /// </summary>
        /// <returns><c>0</c> if no port is available.</returns>
        public static int FindFirstAvailablePort(HashSet<int>? portsInUse, int start, int checkCount)
        {
            var portsToCheck = Enumerable.Range(start, checkCount).ToHashSet();
            portsInUse ??= [.. IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Select(x => x.Port)];

            portsToCheck.ExceptWith(portsInUse);

            var firstFreeUdpPortInRange = portsToCheck.FirstOrDefault();
            return firstFreeUdpPortInRange;
        }

        public static string[] GetDiscordNamedPipes() => Directory.GetFiles(@"\\.\pipe\", "discord-ipc-*");
    }
}
