using Client.Services;

namespace Client;

internal static class Program
{
    public static Dictionary<Type, IReportingService> clients = new();
    public static CancellationTokenSource?            applicationExitCancellationToken;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        applicationExitCancellationToken = new CancellationTokenSource();

        RegisterService(new DiscordNamedPipeService());
        RegisterService(new FactorioFileWatcherService());

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Main());
        Application.ApplicationExit += Application_ApplicationExit;
    }

    private static async void Application_ApplicationExit(object? sender, EventArgs e)
    {
        try
        {
            var emptyProgress = new Progress<LogItem>();
            await Task.WhenAll(clients.Values.Select(c => c.StopClient(emptyProgress, CancellationToken.None)));
            await (applicationExitCancellationToken?.CancelAsync() ?? Task.CompletedTask);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public static void RegisterService<T>(T service) where T : IReportingService =>
        clients[typeof(T)] = service;

    public static void UnregisterService<T>() where T : IReportingService
    {
        clients.Remove(typeof(T), out _);
    }
}
