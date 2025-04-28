using Client.Services;

namespace Client;

internal static class Program
{
    public static readonly List<IReportingService>  Services = [];
    public static          CancellationTokenSource? applicationExitCancellationToken;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        applicationExitCancellationToken = new CancellationTokenSource();

        RegisterService(new DiscordPipeService());
        RegisterService(new FactorioFileWatcherService());

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ApplicationExit += Application_ApplicationExit;
        Application.Run(new Main());
    }

    private static void Application_ApplicationExit(object? sender, EventArgs e)
    {
        try
        {
            var emptyProgress = new Progress<LogItem>();
            for (var i = Services.Count - 1; i >= 0; i--)
            {
                var reportingService = Services[i];
                reportingService.StopClient(emptyProgress, CancellationToken.None).GetAwaiter().GetResult();
            }
            applicationExitCancellationToken?.Cancel();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public static void RegisterService<T>(T service) where T : IReportingService =>
        Services.Add(service);

    public static void UnregisterService<T>() where T : IReportingService
    {
        Services.RemoveAll(c => c.GetType() == typeof(T));
    }

    public static T? GetService<T>() where T : IReportingService =>
        (T?)Services.Find(c => c.GetType() == typeof(T));
}
