using System.ComponentModel.Design;
using Client.Services;

namespace Client;

internal static class Program
{
    public static CancellationToken ApplicationExitCancellationToken => ApplicationExitCancellationTokenSource.Token;

    private static readonly CancellationTokenSource ApplicationExitCancellationTokenSource = new();
    private static readonly ServiceContainer        ServiceContainer                       = new();
    private static          ServicesMarshal?        _servicesMarshal;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ServiceContainer.AddService(typeof(FactorioFileWatcherService),    new FactorioFileWatcherService());
        ServiceContainer.AddService(typeof(DiscordPipeService),            new DiscordPipeService());
        ServiceContainer.AddService(typeof(PositionTransferClientService), new PositionTransferClientService());
        ServiceContainer.AddService(typeof(PositionTransferHostService),   new PositionTransferHostService());
        ServiceContainer.AddService(typeof(PlayerTrackerService),          new PlayerTrackerService());

        _servicesMarshal = new ServicesMarshal(ServiceContainer);

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.ApplicationExit += Application_ApplicationExit;
        Application.Run(new Main(_servicesMarshal));
    }

    private static async void Application_ApplicationExit(object? sender, EventArgs e)
    {
        try
        {
            if (_servicesMarshal != null)
                await _servicesMarshal.StopAsync();

            ServiceContainer.Dispose();
            await ApplicationExitCancellationTokenSource.CancelAsync();
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
