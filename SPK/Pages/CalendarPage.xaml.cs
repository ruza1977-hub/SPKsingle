using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class CalendarPage : Page
{
    private CalendarDay current = new();

    public CalendarPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            YearFilter.Value = DateTime.Today.Year;
            Tarikh.Date = new DateTimeOffset(DateTime.Today);
            Refresh();
        };
    }

    private int SelectedYear => double.IsNaN(YearFilter.Value) ? DateTime.Today.Year : (int)YearFilter.Value;

    private void Refresh()
    {
        var items = DatabaseService.Instance.GetCalendarDays(SelectedYear).Select(x => new CalendarViewRow(x)).ToList();
        List.ItemsSource = items;
        CountText.Text = $"{items.Count} HARI";
    }

    private void YearFilter_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (IsLoaded) Refresh();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        current.Tarikh = Tarikh.Date?.DateTime ?? DateTime.Today;
        current.Jenis = (Jenis.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Hari Persekolahan";
        current.Tajuk = Tajuk.Text;
        current.HariPersekolahan = Hari.IsOn;
        current.MingguAkademik = double.IsNaN(Minggu.Value) ? null : (int)Minggu.Value;
        current.Catatan = Catatan.Text;
        DatabaseService.Instance.SaveCalendarDay(current);
        YearFilter.Value = current.Tarikh.Year;
        FormStatus.Text = "Takwim berjaya disimpan.";
        current = new CalendarDay();
        Refresh();
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not CalendarViewRow row) return;
        current = row.Source;
        Tarikh.Date = new DateTimeOffset(current.Tarikh);
        Jenis.SelectedIndex = Math.Max(0, new[] { "Hari Persekolahan", "Cuti Penggal", "Cuti Umum", "Cuti Perayaan", "Cuti Hujung Minggu" }.ToList().IndexOf(current.Jenis));
        Tajuk.Text = current.Tajuk;
        Hari.IsOn = current.HariPersekolahan;
        Minggu.Value = current.MingguAkademik ?? double.NaN;
        Catatan.Text = current.Catatan;
        FormStatus.Text = "Mod kemas kini.";
    }

    private sealed class CalendarViewRow
    {
        public CalendarDay Source { get; }
        public string Tarikh => Source.Tarikh.ToString("dd/MM/yyyy");
        public string Jenis => Source.Jenis;
        public string Tajuk => Source.Tajuk;
        public string HariLabel => Source.HariPersekolahan ? "YA" : "TIDAK";
        public CalendarViewRow(CalendarDay source) => Source = source;
    }
}
