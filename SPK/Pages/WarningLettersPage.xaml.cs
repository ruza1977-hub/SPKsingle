using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Helpers;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;
using Windows.Storage;
using Windows.System;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class WarningLettersPage : Page
{
    private WarningLetter current = new();
    private List<Student> students = [];
    private bool loading;
    private string? previewPath;

    public WarningLettersPage()
    {
        InitializeComponent();
        Loaded += (_, _) => { LoadStudents(); NewRecord(); Refresh(); };
    }

    private void LoadStudents()
    {
        students = DatabaseService.Instance.GetStudents().Where(x => x.Aktif).ToList();
        StudentPicker.ItemsSource = students;
        WarningType.ItemsSource = new[] { "AMARAN 1", "AMARAN 2", "AMARAN 3", "NOTIS KHAS" };
    }

    private void Refresh()
    {
        var items = DatabaseService.Instance.GetWarningLetters(Search?.Text ?? "");
        List.ItemsSource = items; CountText.Text = $"{items.Count} SURAT";
    }

    private void NewRecord()
    {
        loading = true;
        current = new WarningLetter { IssueDate = DateTime.Today, WarningType = "AMARAN 1", Title = "SURAT AMARAN 1: KETIDAKHADIRAN KE SEKOLAH" };
        StudentPicker.SelectedItem = null; IssueDate.Date = DateTimeOffset.Now; WarningType.SelectedItem = "AMARAN 1";
        ReferenceNo.Text = GuardianName.Text = GuardianAddress.Text = Notes.Text = ""; AbsenceCount.Text = "0"; LetterTitle.Text = current.Title; LetterBody.Text = "";
        List.SelectedItem = null; FormTitle.Text = "Surat Amaran Baharu"; FormStatus.Text = "Pilih murid untuk menjana surat."; ClearPreview();
        loading = false;
    }

    private void RefreshStudentDefaults(bool updateBody)
    {
        if (loading || StudentPicker.SelectedItem is not Student s) return;
        GuardianName.Text = s.Penjaga; GuardianAddress.Text = s.Alamat;
        RefreshAbsenceCount(updateBody);
    }

    private void RefreshAbsenceCount(bool updateBody)
    {
        if (StudentPicker.SelectedItem is not Student s) { AbsenceCount.Text = "0"; return; }
        var date = IssueDate.Date.DateTime;
        var total = DatabaseService.Instance.AttendanceAbsenceCount(s.Id, date); AbsenceCount.Text = total.ToString();
        if (updateBody) LetterBody.Text = DefaultBody(s, total, date);
    }

    private string DefaultBody(Student s, int total, DateTime date) =>
        $"Dengan hormatnya perkara di atas adalah dirujuk. Rekod Sistem Pengurusan Kehadiran menunjukkan bahawa {s.Nama} dari kelas {s.Kelas} mempunyai {total} hari rekod ketidakhadiran sehingga {date:dd/MM/yyyy}. Kerjasama tuan/puan dipohon untuk memastikan murid hadir ke sekolah secara konsisten dan memaklumkan pihak sekolah sekiranya terdapat sebab yang munasabah bagi ketidakhadiran tersebut.\n\nSekiranya tuan/puan memerlukan penjelasan lanjut, sila berhubung dengan pihak sekolah supaya tindakan sokongan yang sesuai dapat dibincangkan bersama.";

    private void StudentPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshStudentDefaults(true);
    private void IssueDate_DateChanged(object sender, DatePickerValueChangedEventArgs e) { if (!loading) RefreshAbsenceCount(string.IsNullOrWhiteSpace(current.Id)); }
    private void WarningType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading) return; var type = WarningType.SelectedItem?.ToString() ?? "AMARAN 1"; LetterTitle.Text = $"SURAT {type}: KETIDAKHADIRAN KE SEKOLAH";
        if (string.IsNullOrWhiteSpace(current.Id)) RefreshAbsenceCount(true);
    }

    private void ResetTemplate_Click(object sender, RoutedEventArgs e)
    {
        var type = WarningType.SelectedItem?.ToString() ?? "AMARAN 1"; LetterTitle.Text = $"SURAT {type}: KETIDAKHADIRAN KE SEKOLAH"; RefreshAbsenceCount(true);
    }
    private void New_Click(object sender, RoutedEventArgs e) => NewRecord();

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (StudentPicker.SelectedItem is not Student s) { FormStatus.Text = "Pilih murid terlebih dahulu."; return; }
        if (string.IsNullOrWhiteSpace(LetterTitle.Text)) { FormStatus.Text = "Tajuk surat mesti diisi."; return; }
        RefreshAbsenceCount(false);
        current.StudentId = s.Id; current.StudentName = s.Nama; current.Kelas = s.Kelas; current.IssueDate = IssueDate.Date.DateTime;
        current.WarningType = WarningType.SelectedItem?.ToString() ?? "AMARAN 1"; current.ReferenceNo = ReferenceNo.Text; current.GuardianName = GuardianName.Text; current.GuardianAddress = GuardianAddress.Text;
        current.TotalAbsences = int.TryParse(AbsenceCount.Text, out var n) ? n : 0; current.Title = LetterTitle.Text; current.Body = string.IsNullOrWhiteSpace(LetterBody.Text) ? DefaultBody(s, current.TotalAbsences, current.IssueDate) : LetterBody.Text; current.Notes = Notes.Text;
        var profile = DatabaseService.Instance.GetSchoolProfile(); current.PdfData = SimplePdfService.WarningLetter(current, s, profile.Name);
        DatabaseService.Instance.SaveWarningLetter(current); Refresh(); await ShowPreview(current.PdfData); FormTitle.Text = "Maklumat Surat"; FormStatus.Text = "Surat berjaya dijana dan disimpan dalam SQLite.";
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private async void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not WarningLetter x) return; current = x; loading = true;
        StudentPicker.SelectedItem = students.FirstOrDefault(s => s.Id == x.StudentId); IssueDate.Date = new DateTimeOffset(x.IssueDate); WarningType.SelectedItem = x.WarningType;
        ReferenceNo.Text = x.ReferenceNo; GuardianName.Text = x.GuardianName; GuardianAddress.Text = x.GuardianAddress; AbsenceCount.Text = x.TotalAbsences.ToString(); LetterTitle.Text = x.Title; LetterBody.Text = x.Body; Notes.Text = x.Notes;
        FormTitle.Text = "Maklumat Surat"; FormStatus.Text = "Rekod surat dibuka."; loading = false; await ShowPreview(x.PdfData);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(current.Id)) return; DatabaseService.Instance.DeleteWarningLetter(current.Id); NewRecord(); Refresh(); FormStatus.Text = "Surat dipadam (soft delete).";
    }

    private async Task ShowPreview(byte[]? data)
    {
        if (data is null || data.Length == 0) { ClearPreview(); return; }
        var dir = AppPaths.Ensure("pdf_preview"); previewPath = Path.Combine(dir, "surat_amaran_preview.pdf"); await File.WriteAllBytesAsync(previewPath, data);
        EmptyPreview.Visibility = Visibility.Collapsed; PdfPreview.Visibility = Visibility.Visible; PdfPreview.Source = new Uri(previewPath);
    }

    private void ClearPreview() { PdfPreview.Source = null; PdfPreview.Visibility = Visibility.Collapsed; EmptyPreview.Visibility = Visibility.Visible; previewPath = null; }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (current.PdfData is null || current.PdfData.Length == 0) { FormStatus.Text = "Jana atau pilih surat terlebih dahulu."; return; }
        var picker = PickerHelper.CreateSavePicker(App.MainWindowInstance!); picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
        var safe = (current.StudentName.Length > 0 ? current.StudentName : "Surat_Amaran").Replace("/", "-").Replace(":", "-"); picker.SuggestedFileName = $"{current.WarningType.Replace(' ', '_')}_{safe}";
        var file = await picker.PickSaveFileAsync(); if (file is null) return; await FileIO.WriteBytesAsync(file, current.PdfData); FormStatus.Text = "PDF berjaya dieksport.";
    }

    private async void OpenPrint_Click(object sender, RoutedEventArgs e)
    {
        if (current.PdfData is null || current.PdfData.Length == 0) { FormStatus.Text = "Jana atau pilih surat terlebih dahulu."; return; }
        await ShowPreview(current.PdfData); if (previewPath is null) return; var file = await StorageFile.GetFileFromPathAsync(previewPath); await Launcher.LaunchFileAsync(file); FormStatus.Text = "PDF dibuka dalam aplikasi lalai. Gunakan Print (Ctrl+P) untuk mencetak.";
    }
}
