using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SistemPengurusanKehadiran.Services;
using SistemPengurusanKehadiran.Helpers;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class SyncPage : Page
{
    private bool loading;

    public SyncPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AutoSyncService.Instance.SyncFinished += AutoSync_SyncFinished;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AutoSyncService.Instance.SyncFinished -= AutoSync_SyncFinished;
    }

    private void Refresh()
    {
        loading = true;
        try
        {
            var cfg = ServerConfigService.Instance.Load();
            ServerText.Text = cfg?.BaseUrl ?? "Server Config belum tersedia";

            var localSchool = DatabaseService.Instance.SchoolId;
            if (cfg is null)
            {
                SchoolGuardText.Text = "Tenant lokal: " + ShortId(localSchool);
                SetConnectionState(false, "Config diperlukan");
            }
            else if (ServerConfigService.Instance.IsReady(localSchool, out _))
            {
                SchoolGuardText.Text = "School ID sepadan · " + ShortId(localSchool);
                ConnectionBadge.Text = "Sedia sync";
                ConnectionDot.Background = (Brush)Application.Current.Resources["MintBrush"];
            }
            else
            {
                SchoolGuardText.Text = "AMARAN: School ID config/token tidak sah untuk SQLite lokal";
                SetConnectionState(false, "Config tidak sah");
            }

            PendingText.Text = DatabaseService.Instance.PendingSyncCount().ToString();
            ConflictText.Text = DatabaseService.Instance.ConflictCount().ToString();
            RevisionText.Text = ValueOrZero(DatabaseService.Instance.GetSyncState("last_pull_revision"));

            QueueList.ItemsSource = DatabaseService.Instance.GetPendingSyncByTable();
            ConflictList.ItemsSource = DatabaseService.Instance.GetOpenSyncConflicts(50);

            var last = DatabaseService.Instance.GetSyncState("last_sync_at");
            LastSyncText.Text = FormatUtc(last);
            DeviceText.Text = ValueOrDash(DatabaseService.Instance.GetSyncState("device_id"));

            AutoSyncToggle.IsOn = AutoSyncService.Instance.Enabled;
            SelectInterval(AutoSyncService.Instance.IntervalMinutes);

            var autoError = DatabaseService.Instance.GetSyncState("last_auto_sync_error");
            var autoAt = DatabaseService.Instance.GetSyncState("last_auto_sync_at");
            AutoStatusText.Text = !string.IsNullOrWhiteSpace(autoError)
                ? "Auto Sync: " + autoError
                : string.IsNullOrWhiteSpace(autoAt)
                    ? "Auto Sync belum berjalan"
                    : "Auto Sync terakhir: " + FormatUtc(autoAt);
        }
        finally
        {
            loading = false;
        }
    }

    private async void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var window = App.MainWindowInstance;
        if (window is null)
        {
            Status.Text = "Tetingkap utama tidak tersedia.";
            return;
        }

        var picker = PickerHelper.CreateOpenPicker(window);
        picker.FileTypeFilter.Add(".zip");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            ServerConfigService.Instance.ImportZip(file.Path);

            if (!ServerConfigService.Instance.IsReady(DatabaseService.Instance.SchoolId, out var reason))
            {
                Refresh();
                Status.Text = "Server Config diimport tetapi belum boleh sync: " + reason;
                SetConnectionState(false, "Config tidak sah");
                return;
            }

            // Config ialah credential sambungan. Selepas import sekali, terus aktif dan sync.
            AutoSyncService.Instance.SetEnabled(true);
            Refresh();
            Status.Text = "Server Config sedia. Sync pertama sedang dijalankan…";
            SetConnectionState(true, "Sedia sync");
            await AutoSyncService.Instance.TriggerNowAsync();
            Refresh();
        }
        catch (Exception ex)
        {
            Status.Text = "Import Server Config gagal: " + ex.Message;
            SetConnectionState(false, "Config gagal");
        }
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        await RunBusy(async () =>
        {
            Status.Text = "Menguji sambungan Railway…";
            var result = await RailwaySyncService.Instance.TestAsync();
            Status.Text = result;
            SetConnectionState(true, "Online");
        });
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        await RunBusy(async () =>
        {
            Status.Text = "Sync sedang berjalan…";
            var r = await RailwaySyncService.Instance.SyncAsync();
            Status.Text = $"Sync selesai · push {r.Pushed} · pull {r.Pulled} · konflik {r.Conflicts} · revision {r.Revision}";
            SetConnectionState(true, "Online");
            Refresh();
        });
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void AutoSync_Toggled(object sender, RoutedEventArgs e)
    {
        if (loading) return;
        AutoSyncService.Instance.SetEnabled(AutoSyncToggle.IsOn);
        Status.Text = AutoSyncToggle.IsOn
            ? $"Auto Sync diaktifkan setiap {AutoSyncService.Instance.IntervalMinutes} minit."
            : "Auto Sync dimatikan.";
        Refresh();
    }

    private void Interval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || IntervalCombo.SelectedItem is not ComboBoxItem item) return;
        if (int.TryParse(item.Tag?.ToString(), out var minutes))
        {
            AutoSyncService.Instance.SetIntervalMinutes(minutes);
            Status.Text = $"Selang Auto Sync ditetapkan kepada {minutes} minit.";
        }
    }

    private async Task RunBusy(Func<Task> action)
    {
        SyncSpinner.IsActive = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Status.Text = "Gagal: " + ex.Message;
            SetConnectionState(false, "Tidak tersambung");
        }
        finally
        {
            SyncSpinner.IsActive = false;
            Refresh();
        }
    }

    private void AutoSync_SyncFinished(object? sender, AutoSyncEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Status.Text = e.Success
                ? $"Auto Sync selesai · push {e.Result?.Pushed ?? 0} · pull {e.Result?.Pulled ?? 0} · konflik {e.Result?.Conflicts ?? 0}"
                : "Auto Sync: " + e.Message;
            if (e.Success) SetConnectionState(true, "Online");
            Refresh();
        });
    }

    private void SetConnectionState(bool online, string text)
    {
        ConnectionBadge.Text = text;
        var key = online ? "MintBrush" : "CoralBrush";
        ConnectionDot.Background = (Brush)Application.Current.Resources[key];
    }

    private void SelectInterval(int minutes)
    {
        foreach (var x in IntervalCombo.Items.OfType<ComboBoxItem>())
        {
            if (x.Tag?.ToString() == minutes.ToString())
            {
                IntervalCombo.SelectedItem = x;
                return;
            }
        }
        IntervalCombo.SelectedIndex = 1;
    }

    private static string ValueOrZero(string value) => string.IsNullOrWhiteSpace(value) ? "0" : value;
    private static string ValueOrDash(string value) => string.IsNullOrWhiteSpace(value) ? "Belum dijana" : value;
    private static string ShortId(string value) => value.Length <= 12 ? value : value[..8] + "…" + value[^4..];

    private static string FormatUtc(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Belum pernah sync";
        return DateTimeOffset.TryParse(value, out var dt)
            ? dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
            : value;
    }
}
