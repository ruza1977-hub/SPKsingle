using Microsoft.UI.Xaml;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine(e.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DatabaseService.Instance.Initialize();

        // Jika server_config.zip sudah dibekalkan bersama aplikasi, import sekali secara automatik.
        ServerConfigService.Instance.TryAutoImportPackagedConfig();
        AutoSyncService.Instance.InitializeFromServerConfig();

        MainWindowInstance = new MainWindow();
        MainWindowInstance.Closed += (_, _) => AutoSyncService.Instance.Stop();
        MainWindowInstance.Activate();

        // Jika config sah sudah tersedia dan Auto Sync aktif, sync pertama berjalan selepas ~3 saat.
        AutoSyncService.Instance.Start();
    }
}
