using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class RecoveryPage : Page
{
    private CaseProfile? current;
    private List<Teacher> teachers = [];
    private bool suppressSelectionEvents;

    public RecoveryPage()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializePage();
    }

    private void InitializePage()
    {
        teachers = DatabaseService.Instance.GetTeachers().Where(x => x.Aktif).ToList();
        TeacherPicker.ItemsSource = teachers;

        Filter.ItemsSource = new[] { "AKTIF", "PEMANTAUAN", "DIPULIHKAN", "SEMUA" };
        Filter.SelectedItem = "AKTIF";

        RecoveredAt.Date = DateTimeOffset.Now;
        MonitoringUntil.Date = DateTimeOffset.Now.AddDays(30);
        ClearForm();
        Refresh(true);
    }

    private void Refresh(bool rebuildStudentDropdown = false)
    {
        var filter = Filter.SelectedItem?.ToString() ?? "AKTIF";
        var allForFilter = DatabaseService.Instance.GetRecoveryCases("", filter);

        if (rebuildStudentDropdown)
            RebuildStudentDropdown(allForFilter);

        var selectedStudentId = (StudentSearch.SelectedItem as RecoveryStudentOption)?.StudentId ?? "";
        var items = string.IsNullOrWhiteSpace(selectedStudentId)
            ? allForFilter
            : allForFilter.Where(x => x.StudentId == selectedStudentId).ToList();

        List.ItemsSource = items;

        var summary = DatabaseService.Instance.GetRecoverySummary();
        ActiveText.Text = summary.Active.ToString();
        MonitoringText.Text = summary.Monitoring.ToString();
        RecoveredText.Text = summary.Recovered.ToString();
        RecoveredCount.Text = $"{summary.Recovered} DIPULIHKAN";

        // Jika dropdown memilih seorang murid yang hanya mempunyai satu kes, pilih terus rekod itu.
        if (!string.IsNullOrWhiteSpace(selectedStudentId) && items.Count == 1)
            List.SelectedItem = items[0];
    }

    private void RebuildStudentDropdown(List<CaseProfile> cases)
    {
        var previousStudentId = (StudentSearch.SelectedItem as RecoveryStudentOption)?.StudentId ?? "";
        var options = cases
            .GroupBy(x => x.StudentId)
            .Select(g => g.OrderBy(x => x.StudentName).First())
            .OrderBy(x => x.StudentName)
            .ThenBy(x => x.Kelas)
            .Select(x => new RecoveryStudentOption
            {
                StudentId = x.StudentId,
                Display = x.StudentDisplay
            })
            .ToList();

        options.Insert(0, new RecoveryStudentOption { StudentId = "", Display = "SEMUA MURID" });

        suppressSelectionEvents = true;
        StudentSearch.ItemsSource = options;
        StudentSearch.SelectedItem = options.FirstOrDefault(x => x.StudentId == previousStudentId) ?? options[0];
        suppressSelectionEvents = false;
    }

    private void ClearForm()
    {
        current = null;
        List.SelectedItem = null;
        SelectedStudent.Text = "Pilih satu kes di sebelah kiri";
        SelectedStatus.Text = "TIADA KES";
        TeacherPicker.SelectedItem = null;
        Outcome.Text = "";
        RecoveryNotes.Text = "";
        RecoveredAt.Date = DateTimeOffset.Now;
        MonitoringUntil.Date = DateTimeOffset.Now.AddDays(30);
        NoMonitoringDate.IsChecked = false;
        FormStatus.Text = "Pilih murid daripada dropdown atau senarai, kemudian tekan Tandakan Dipulihkan.";
    }

    private void StudentSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionEvents || !IsLoaded) return;
        ClearForm();
        Refresh(false);
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ClearForm();
        Refresh(true);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ClearForm();
        Refresh(true);
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not CaseProfile x) return;

        current = x;
        SelectedStudent.Text = $"{x.StudentName} • {x.Kelas} • {x.Category}";
        SelectedStatus.Text = x.Status;
        RecoveredAt.Date = new DateTimeOffset(x.RecoveredAt ?? DateTime.Today);
        TeacherPicker.SelectedItem = teachers.FirstOrDefault(t => t.Id == x.RecoveryTeacherId);
        Outcome.Text = x.RecoveryOutcome;
        RecoveryNotes.Text = x.RecoveryNotes;
        MonitoringUntil.Date = new DateTimeOffset(x.MonitoringUntil ?? DateTime.Today.AddDays(30));
        NoMonitoringDate.IsChecked = x.MonitoringUntil is null;
        FormStatus.Text = x.Status == "SELESAI"
            ? "Rekod ini telah ditandakan dipulihkan."
            : "Rekod sedia untuk proses pemulihan.";
    }

    private void Recover_Click(object sender, RoutedEventArgs e)
    {
        if (current is null)
        {
            FormStatus.Text = "Pilih satu kes terlebih dahulu.";
            return;
        }

        if (current.Status == "SELESAI")
        {
            FormStatus.Text = "Kes ini sudah berstatus DIPULIHKAN.";
            return;
        }

        // Hasil pemulihan adalah pilihan. Jika guru mahu satu klik sahaja, sistem isi nilai asas.
        var outcome = string.IsNullOrWhiteSpace(Outcome.Text) ? "PULIH" : Outcome.Text.Trim();
        var teacherId = (TeacherPicker.SelectedItem as Teacher)?.Id ?? "";
        DateTime? monitor = NoMonitoringDate.IsChecked == true ? null : MonitoringUntil.Date.DateTime;

        var changed = DatabaseService.Instance.MarkCaseRecovered(
            current.Id,
            RecoveredAt.Date.DateTime,
            outcome,
            teacherId,
            monitor,
            RecoveryNotes.Text);

        if (changed != 1)
        {
            FormStatus.Text = "Pemulihan tidak disimpan. Tekan Segar Semula dan cuba lagi.";
            return;
        }

        // Selepas berjaya, paparan AKTIF mesti terus berkurang dan kes yang dipulihkan hilang daripada senarai aktif.
        var message = $"{current.StudentName} berjaya ditandakan DIPULIHKAN.";
        current = null;
        List.SelectedItem = null;
        Refresh(true);
        FormStatus.Text = message;
    }

    private void Monitoring_Click(object sender, RoutedEventArgs e)
    {
        if (current is null)
        {
            FormStatus.Text = "Pilih satu kes terlebih dahulu.";
            return;
        }

        DateTime? monitor = NoMonitoringDate.IsChecked == true ? null : MonitoringUntil.Date.DateTime;
        DatabaseService.Instance.ReturnCaseToMonitoring(current.Id, monitor, RecoveryNotes.Text);

        var message = $"{current.StudentName} dikembalikan kepada status PEMANTAUAN.";
        current = null;
        List.SelectedItem = null;
        Refresh(true);
        FormStatus.Text = message;
    }
}
