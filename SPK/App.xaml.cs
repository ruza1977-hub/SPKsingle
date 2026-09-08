using Microsoft.UI.Xaml;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SistemPengurusanKehadiran", "logs");

    private static string StartupLog => Path.Combine(LogDirectory, "startup.log");

    public App()
    {
        InitializeComponent();
        Log("App constructor OK");

        UnhandledException += (_, e) =>
        {
            Log("UnhandledException: " + e.Exception);
            // Jangan tutup aplikasi secara senyap. Rekodkan dahulu untuk diagnosis.
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Log("OnLaunched begin");

            DatabaseService.Instance.Initialize();
            Log("DatabaseService.Initialize OK");

            try
            {
                var imported = ServerConfigService.Instance.TryAutoImportPackagedConfig();
                Log("ServerConfig auto import OK" + (imported is null ? "" : $": {imported}"));
            }
            catch (Exception ex)
            {
                Log("ServerConfig auto import warning: " + ex);
            }

            try
            {
                AutoSyncService.Instance.InitializeFromServerConfig();
                Log("AutoSync initialize OK");
            }
            catch (Exception ex)
            {
                Log("AutoSync initialize warning: " + ex);
            }

            MainWindowInstance = new MainWindow();
            MainWindowInstance.Closed += (_, _) => AutoSyncService.Instance.Stop();
            MainWindowInstance.Activate();
            Log("MainWindow activated");

            try
            {
                AutoSyncService.Instance.Start();
                Log("AutoSync start OK");
            }
            catch (Exception ex)
            {
                Log("AutoSync start warning: " + ex);
            }
        }
        catch (Exception ex)
        {
            Log("FATAL startup error: " + ex);
            throw;
        }
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                StartupLog,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging tidak boleh menggagalkan aplikasi.
        }
    }
}
