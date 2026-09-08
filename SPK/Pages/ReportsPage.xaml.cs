using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Helpers;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;
using System.Globalization;
using System.Text;
using Windows.Storage;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class ReportsPage : Page
{
    private bool _ready;
    private AnalyticsReportData _current = new();

    public ReportsPage()
    {
        InitializeComponent();
        Loaded += ReportsPage_Loaded;
    }

    private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_ready) return;

        var now = DateTime.Today;
        for (var year = now.Year - 2; year <= now.Year + 2; year++) YearBox.Items.Add(year.ToString(CultureInfo.InvariantCulture));
        YearBox.SelectedItem = now.Year.ToString(CultureInfo.InvariantCulture);
        PeriodBox.SelectedIndex = 0;

        ClassBox.Items.Add("SEMUA KELAS");
        foreach (var kelas in DatabaseService.Instance.GetClasses()) ClassBox.Items.Add(kelas);
        ClassBox.SelectedIndex = 0;

        foreach (var type in ReportBuilderService.ReportTypes) ReportTypeBox.Items.Add(type);
        ReportTypeBox.SelectedIndex = 0;
        UpdateReportDescription();

        _ready = true;
        RefreshReport();
    }

    private void ReportType_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateReportDescription();
    }

    private void UpdateReportDescription()
    {
        if (ReportDescriptionText is null || ReportTypeBox is null) return;
        ReportDescriptionText.Text = ReportBuilderService.Description(ReportTypeBox.SelectedItem?.ToString() ?? "Ringkasan & Analitik Kehadiran");
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) RefreshReport();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshReport();

    private void RefreshReport()
    {
        try
        {
            var year = SelectedYear();
            var period = PeriodBox.SelectedItem?.ToString() ?? "SEPANJANG TAHUN";
            var kelas = ClassBox.SelectedItem?.ToString() ?? "SEMUA KELAS";
            var (start, end) = ResolvePeriod(year, period);

            _current = DatabaseService.Instance.GetAnalyticsReport(start, end, kelas);

            RateValue.Text = $"{_current.AttendanceRate:0.0}%";
            AbsentValue.Text = _current.NotPresentRecords.ToString(CultureInfo.InvariantCulture);
            PontengValue.Text = _current.PontengRecords.ToString(CultureInfo.InvariantCulture);
            ActiveCaseValue.Text = _current.ActiveCases.ToString(CultureInfo.InvariantCulture);
            RecoveredValue.Text = _current.RecoveredCases.ToString(CultureInfo.InvariantCulture);

            TrendList.ItemsSource = _current.Trend;
            TrendEmpty.Visibility = _current.Trend.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            ClassPerformanceList.ItemsSource = _current.Classes;
            RiskList.ItemsSource = _current.RiskStudents;
            RiskEmpty.Visibility = _current.RiskStudents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            StatusText.Text = $"{FormatPeriod(start, end)} · {kelas} · {_current.TotalRecords} rekod kehadiran";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Analitik gagal dimuatkan: " + ex.Message;
        }
    }

    private int SelectedYear() => int.TryParse(YearBox.SelectedItem?.ToString(), out var year) ? year : DateTime.Today.Year;

    private static (DateTime Start, DateTime End) ResolvePeriod(int year, string period)
    {
        if (period == "JAN - JUN") return (new DateTime(year, 1, 1), new DateTime(year, 6, 30));
        if (period == "JUL - DIS") return (new DateTime(year, 7, 1), new DateTime(year, 12, 31));
        if (period == "BULAN SEMASA")
        {
            var month = DateTime.Today.Month;
            var start = new DateTime(year, month, 1);
            return (start, start.AddMonths(1).AddDays(-1));
        }
        return (new DateTime(year, 1, 1), new DateTime(year, 12, 31));
    }

    private static string FormatPeriod(DateTime start, DateTime end)
    {
        var ms = new CultureInfo("ms-MY");
        return start.Month == 1 && start.Day == 1 && end.Month == 12 && end.Day == 31
            ? $"Tahun {start.Year}"
            : $"{start.ToString("dd MMM yyyy", ms)} – {end.ToString("dd MMM yyyy", ms)}";
    }

    private ReportDocument BuildSelectedReport()
    {
        var type = ReportTypeBox.SelectedItem?.ToString() ?? "Ringkasan & Analitik Kehadiran";
        var (start, end) = ResolvePeriod(SelectedYear(), PeriodBox.SelectedItem?.ToString() ?? "SEPANJANG TAHUN");
        var kelas = ClassBox.SelectedItem?.ToString() ?? "SEMUA KELAS";
        return ReportBuilderService.Build(type, start, end, kelas);
    }

    private async void ExportWordReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = BuildSelectedReport();
            var picker = PickerHelper.CreateSavePicker(App.MainWindowInstance!);
            picker.FileTypeChoices.Add("Microsoft Word", new List<string> { ".docx" });
            picker.SuggestedFileName = SafeReportFileName(report.Title, report.ClassText);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await FileIO.WriteBytesAsync(file, WordReportService.Build(report));
            StatusText.Text = $"Word berjaya dijana: {file.Name} · {report.Rows.Count} rekod";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Jana Word gagal: " + ex.Message;
        }
    }

    private async void ExportSelectedPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = BuildSelectedReport();
            var picker = PickerHelper.CreateSavePicker(App.MainWindowInstance!);
            picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
            picker.SuggestedFileName = SafeReportFileName(report.Title, report.ClassText);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await FileIO.WriteBytesAsync(file, SimplePdfService.GenericReport(report));
            StatusText.Text = $"PDF berjaya dijana: {file.Name} · {report.Rows.Count} rekod";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Jana PDF gagal: " + ex.Message;
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshReport();
            var picker = PickerHelper.CreateSavePicker(App.MainWindowInstance!);
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
            picker.SuggestedFileName = $"Laporan_Analitik_{SelectedYear()}_{SafeFilePart(ClassBox.SelectedItem?.ToString() ?? "Semua_Kelas")}";
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            await FileIO.WriteTextAsync(file, BuildCsv(), Windows.Storage.Streams.UnicodeEncoding.Utf8);
            StatusText.Text = "CSV berjaya dieksport: " + file.Name;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Eksport CSV gagal: " + ex.Message;
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshReport();
            var picker = PickerHelper.CreateSavePicker(App.MainWindowInstance!);
            picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
            picker.SuggestedFileName = $"Laporan_Analitik_{SelectedYear()}_{SafeFilePart(ClassBox.SelectedItem?.ToString() ?? "Semua_Kelas")}";
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            var school = DatabaseService.Instance.GetSchoolProfile();
            var period = FormatPeriod(_current.StartDate, _current.EndDate);
            var bytes = SimplePdfService.AnalyticsReport(_current, school.Name, period);
            await FileIO.WriteBytesAsync(file, bytes);
            StatusText.Text = "PDF analitik berjaya dieksport: " + file.Name;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Eksport PDF gagal: " + ex.Message;
        }
    }

    private string BuildCsv()
    {
        var sb = new StringBuilder();
        void Row(params object?[] values) => sb.AppendLine(string.Join(",", values.Select(Csv)));

        var school = DatabaseService.Instance.GetSchoolProfile();
        Row("LAPORAN & ANALITIK", school.Name);
        Row("Tempoh", FormatPeriod(_current.StartDate, _current.EndDate));
        Row("Kelas", _current.ClassFilter);
        Row();
        Row("RINGKASAN", "Nilai");
        Row("Kadar Hadir", $"{_current.AttendanceRate:0.0}%");
        Row("Tidak Hadir", _current.NotPresentRecords);
        Row("Ponteng", _current.PontengRecords);
        Row("Kes Aktif", _current.ActiveCases);
        Row("Dipulihkan", _current.RecoveredCases);
        Row();
        Row("TREND KEHADIRAN");
        Row("Bulan", "Jumlah Rekod", "Hadir + Lewat", "% Hadir");
        foreach (var x in _current.Trend) Row(x.MonthLabel, x.Total, x.Present, x.AttendanceRateText);
        Row();
        Row("PRESTASI MENGIKUT KELAS");
        Row("Kelas", "Tidak Hadir", "Ponteng", "Jumlah Rekod", "% Hadir");
        foreach (var x in _current.Classes) Row(x.Kelas, x.TidakHadir, x.Ponteng, x.Total, x.AttendanceRateText);
        Row();
        Row("MURID BERISIKO");
        Row("Murid", "Kelas", "TH", "Ponteng", "Lewat", "% Hadir", "Risiko", "Kes");
        foreach (var x in _current.RiskStudents) Row(x.Nama, x.Kelas, x.TidakHadir, x.Ponteng, x.Lewat, x.AttendanceRateText, x.RiskText, x.CaseText);
        return sb.ToString();
    }

    private static string Csv(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return '"' + text.Replace("\"", "\"\"") + '"';
    }

    private static string SafeReportFileName(string title, string kelas)
        => $"SPK_{SafeFilePart(title)}_{DateTime.Now:yyyyMMdd}_{SafeFilePart(kelas)}";

    private static string SafeFilePart(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(text.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) || c == '/' ? '_' : c).ToArray());
    }
}
