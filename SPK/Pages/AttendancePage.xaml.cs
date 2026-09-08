using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class AttendancePage : Page
{
    private readonly ObservableCollection<AttendanceRow> rows = new();

    public AttendancePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
    }

    private void Init()
    {
        Tarikh.Date = new DateTimeOffset(DateTime.Today);
        Kelas.Items.Clear();
        Kelas.Items.Add("PILIH KELAS");
        foreach (var kelas in DatabaseService.Instance.GetClasses()) Kelas.Items.Add(kelas);
        Kelas.Items.Add("SEMUA KELAS");
        Kelas.SelectedIndex = 0;
        List.ItemsSource = rows;
        Refresh();
    }

    private void Selection_Changed(object sender, object e) => Refresh();

    private void Refresh()
    {
        rows.Clear();
        UpdateCounters();

        var date = Tarikh.Date?.DateTime ?? DateTime.Today;
        var schoolDay = DatabaseService.Instance.IsSchoolDay(date);

        if (schoolDay != true)
        {
            List.Visibility = Visibility.Collapsed;
            Empty.Visibility = Visibility.Visible;
            EmptyTitle.Text = schoolDay == false ? "Tarikh ini bukan hari persekolahan" : "Tarikh belum ditemui dalam Takwim";
            EmptyMessage.Text = schoolDay == false ? "Senarai murid tidak dipaparkan pada hari cuti / bukan hari sekolah." : "Import atau lengkapkan Takwim sebelum merekod kehadiran.";
            Status.Text = "Kehadiran dikunci";
            return;
        }

        if (Kelas.SelectedItem?.ToString() is not string kelas || kelas == "PILIH KELAS")
        {
            List.Visibility = Visibility.Collapsed;
            Empty.Visibility = Visibility.Visible;
            EmptyTitle.Text = "Pilih kelas untuk merekod kehadiran";
            EmptyMessage.Text = "Selepas kelas dipilih, semua murid yang belum mempunyai rekod akan ditanda HADIR secara automatik.";
            Status.Text = "Pilih kelas";
            return;
        }

        foreach (var row in DatabaseService.Instance.LoadAttendance(date, kelas)) rows.Add(row);
        List.Visibility = Visibility.Visible;
        Empty.Visibility = Visibility.Collapsed;
        Status.Text = $"{rows.Count} murid • auto HADIR";
        UpdateCounters();
    }

    private void UpdateCounters()
    {
        StudentCount.Text = $"{rows.Count} MURID";
        PresentCount.Text = $"HADIR {rows.Count(x => x.Status is "HADIR" or "LEWAT")}";
        AbsentCount.Text = $"TIDAK HADIR {rows.Count(x => x.Status is "TIDAK HADIR" or "PONTENG" or "MC" or "CUTI BERSEBAB")}";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (rows.Count == 0) return;
        DatabaseService.Instance.SaveAttendance(Tarikh.Date?.DateTime ?? DateTime.Today, rows);
        UpdateCounters();
        Status.Text = $"Disimpan • {rows.Count} rekod";
    }
}
