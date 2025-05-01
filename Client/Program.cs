using System.Configuration;
using dotenv.net;
using dotenv.net.Utilities;

namespace Client;

internal static class Program
{
    public static CancellationToken ApplicationExitCancellationToken => ApplicationExitCancellationTokenSource.Token;

    public static readonly CancellationTokenSource ApplicationExitCancellationTokenSource = new();

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        DotEnv.Load();

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.Run(new Main());
    }

    public static string GetConfig(string key)
    {
        if (ConfigurationManager.AppSettings["UseEnvironmentFile"] == "true")
            return EnvReader.GetStringValue(key);

        return ConfigurationManager.AppSettings[key] ?? string.Empty;
    }
}
