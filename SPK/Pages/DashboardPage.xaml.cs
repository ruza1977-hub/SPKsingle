using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        LoadSchoolLogo();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AutoSyncService.Instance.SyncFinished += SyncFinished;
        Refresh();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
        => AutoSyncService.Instance.SyncFinished -= SyncFinished;

    private void LoadSchoolLogo()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "SchoolLogo.png");
            if (File.Exists(path)) DashboardSchoolLogo.Source = new BitmapImage(new Uri(path));
        }
        catch { }
    }

    private void Refresh()
    {
        var s = DatabaseService.Instance.GetDashboardStats();
        var profile = DatabaseService.Instance.GetSchoolProfile();
        var teacher = DatabaseService.Instance.GetSyncState("teacher_id");
        StudentsText.Text = s.ActiveStudents.ToString();
        TeachersText.Text = s.ActiveTeachers.ToString();
        SchoolDaysText.Text = s.SchoolDaysThisYear.ToString();
        HadirText.Text = s.HadirToday.ToString();
        AbsentText.Text = s.TidakHadirToday.ToString();
        SyncText.Text = s.PendingSync.ToString();
        CaseText.Text = $"Kes aktif: {s.ActiveCases}   •   Dipulihkan: {s.RecoveredCases}";
        IdentityText.Text = $"{profile.Name}  •  ID Guru: {(string.IsNullOrWhiteSpace(teacher) ? "Legacy/Belum ditetapkan" : teacher)}";
        DeviceText.Text = $"{Environment.MachineName}\n{ShortId(ProvisioningService.Instance.DeviceId)}";
        var last = DatabaseService.Instance.GetSyncState("last_sync_at");
        LastSyncText.Text = FormatSync(last);
        var ready = ServerConfigService.Instance.IsReady(DatabaseService.Instance.SchoolId, out var reason);
        ConnectionText.Text = ready ? (s.PendingSync == 0 ? "✓ Diselaraskan" : $"⏳ {s.PendingSync} menunggu") : "Offline / belum disambung";
        SyncNowButton.IsEnabled = ready;
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        SyncNowButton.IsEnabled = false;
        ConnectionText.Text = "Menyelaraskan…";
        try { await AutoSyncService.Instance.TriggerNowAsync(); }
        catch { }
        finally { Refresh(); }
    }

    private void SyncFinished(object? sender, AutoSyncEventArgs e)
        => DispatcherQueue.TryEnqueue(Refresh);

    private void Attendance_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(AttendancePage));
    private void Guardian_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(GuardianContactsPage));
    private void Evidence_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(EvidencePage));

    private static string ShortId(string value) => value.Length <= 12 ? value : value[..12] + "…";
    private static string FormatSync(string iso)
    {
        if (!DateTime.TryParse(iso, out var d)) return "Belum sync";
        return "Sync terakhir: " + d.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }
}
