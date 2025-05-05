using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Client
{
    internal static class AddressUtility
    {
        public static HashSet<ushort> GetActiveUdpPorts() =>
            IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Select(x => (ushort)x.Port).ToHashSet();

        /// <summary>
        /// https://stackoverflow.com/questions/5879605/udp-port-open-check
        /// </summary>
        /// <returns><c>0</c> if no port is available.</returns>
        public static ushort FindFirstAvailablePort(HashSet<ushort>? portsInUse, int start, int checkCount)
        {
            var portsToCheck = Enumerable.Range(start, checkCount).Select(x => (ushort)x).ToHashSet();
            portsInUse ??= GetActiveUdpPorts();

            portsToCheck.ExceptWith(portsInUse);

            var firstFreeUdpPortInRange = portsToCheck.FirstOrDefault();
            return firstFreeUdpPortInRange;
        }

        public static bool CheckUrlReservation(int port)
        {
            var urlToCheck = $"http://+:{port}/";

            try
            {
                var psi = new ProcessStartInfo("netsh", $"http show urlacl url={urlToCheck}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                var       output  = process!.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new Win32Exception(process.ExitCode);

                // Check each line for exact URL match
                foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                    if (line.Contains(urlToCheck, StringComparison.OrdinalIgnoreCase))
                        return true;

                return false;
            }
            catch (Win32Exception ex)
            {
                throw new Exception("Error checking URL reservation", ex);
            }
        }

        public static bool AddUrlReservation(int port, string user = "")
        {
            // User can be Everyone, but we're just going to set it to the current user for now
            if (string.IsNullOrEmpty(user))
                user = $"{Environment.UserDomainName}\\{Environment.UserName}";

            var url = $"http://+:{port}/";
            return ExecuteNetshCommand($"http add urlacl url={url} user={user}");
        }

        private static bool ExecuteNetshCommand(string arguments)
        {
            var psi = new ProcessStartInfo("netsh", arguments)
            {
                Verb            = "runas",
                UseShellExecute = true,
                WindowStyle     = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
    }
}
