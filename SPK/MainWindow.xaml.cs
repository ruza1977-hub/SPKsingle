using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Pages;
using SistemPengurusanKehadiran.Services;
using WinRT.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SistemPengurusanKehadiran;
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadSchoolLogo();
        ExtendsContentIntoTitleBar = false;
        var hwnd=WindowNative.GetWindowHandle(this);var id=Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);var aw=AppWindow.GetFromWindowId(id);aw.Resize(new Windows.Graphics.SizeInt32(1500,920));
        if (ProvisioningService.Instance.IsProvisioned)
        {
            ContentFrame.Navigate(typeof(DashboardPage));
            Nav.SelectedItem=Nav.MenuItems[0];
        }
        else
        {
            ContentFrame.Navigate(typeof(ProvisioningPage));
        }
    }
    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if(args.SelectedItemContainer?.Tag is not string tag)return;
        Type page = tag switch {
            "dashboard"=>typeof(DashboardPage),"import"=>typeof(ImportDataPage),"students"=>typeof(StudentsPage),"teachers"=>typeof(TeachersPage),"calendar"=>typeof(CalendarPage),"attendance"=>typeof(AttendancePage),"reports"=>typeof(ReportsPage),
            "cases"=>typeof(CaseProfilesPage),"contacts"=>typeof(GuardianContactsPage),"visits"=>typeof(HomeVisitsPage),"counselling"=>typeof(CounsellingPage),"interventions"=>typeof(InterventionsPage),"letters"=>typeof(WarningLettersPage),
            "evidence"=>typeof(EvidencePage),"videos"=>typeof(VideoEvidencePage),"recovery"=>typeof(RecoveryPage),
            "provision"=>typeof(ProvisioningPage),"sync"=>typeof(SyncPage),"backup"=>typeof(BackupRestorePage),"settings"=>typeof(SettingsPage),_=>typeof(ModulePreviewPage)};
        if(page==typeof(ModulePreviewPage)) ContentFrame.Navigate(page, tag); else ContentFrame.Navigate(page);
    }

    public void ShowDashboardAfterProvisioning()
    {
        ContentFrame.Navigate(typeof(DashboardPage));
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    private void LoadSchoolLogo()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "SchoolLogo.png");
            if (File.Exists(path)) TopSchoolLogo.Source = new BitmapImage(new Uri(path));
        }
        catch
        {
            // Logo tidak menghalang aplikasi daripada bermula.
        }
    }
}
