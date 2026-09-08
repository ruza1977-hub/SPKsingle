using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SistemPengurusanKehadiran.Helpers;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;
using Windows.Storage;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class EvidencePage : Page
{
    private EvidenceFileRecord current = new();
    private byte[] pendingBytes = Array.Empty<byte>();
    private string pendingFileName = "";
    private string pendingMime = "application/octet-stream";

    public EvidencePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            StudentPicker.ItemsSource = DatabaseService.Instance.GetStudents();
            EvidenceDate.Date = DateTimeOffset.Now;
            Refresh();
        };
    }

    private void Refresh()
    {
        var items = DatabaseService.Instance.GetEvidenceFiles(SearchBox?.Text ?? "");
        List.ItemsSource = items;
        CountText.Text = $"{items.Count} EVIDENS";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        current = new EvidenceFileRecord();
        pendingBytes = Array.Empty<byte>();
        pendingFileName = "";
        pendingMime = "application/octet-stream";
        StudentPicker.SelectedItem = null;
        EvidenceDate.Date = DateTimeOffset.Now;
        EvidenceType.SelectedIndex = 0;
        TitleBox.Text = NotesBox.Text = "";
        SelectedFileText.Text = "Belum ada fail dipilih";
        SelectedFileSizeText.Text = "";
        List.SelectedItem = null;
        ClearPreview();
        FormStatus.Text = "Rekod evidens baharu.";
    }

    private async void PickFile_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindowInstance is null) return;
        var picker = PickerHelper.CreateOpenPicker(App.MainWindowInstance);
        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        pendingBytes = await File.ReadAllBytesAsync(file.Path);
        pendingFileName = file.Name;
        pendingMime = MimeFor(file.FileType);
        SelectedFileText.Text = file.Name;
        SelectedFileSizeText.Text = FormatSize(pendingBytes.LongLength);
        if (string.IsNullOrWhiteSpace(TitleBox.Text)) TitleBox.Text = Path.GetFileNameWithoutExtension(file.Name);
        if (file.FileType.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && EvidenceType.SelectedIndex == 0) EvidenceType.SelectedIndex = 1;
        await PreviewBytesAsync(pendingBytes, pendingFileName, pendingMime, "pending");
        FormStatus.Text = "Fail dipilih. Tekan Simpan untuk masukkan ke SQLite.";
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (StudentPicker.SelectedItem is not Student student)
        {
            FormStatus.Text = "Pilih murid terlebih dahulu.";
            return;
        }
        if (EvidenceType.SelectedItem is not string type || string.IsNullOrWhiteSpace(type))
        {
            FormStatus.Text = "Pilih jenis evidens.";
            return;
        }

        if (pendingBytes.Length == 0 && !string.IsNullOrWhiteSpace(current.Id))
        {
            var existing = DatabaseService.Instance.GetEvidenceFile(current.Id);
            if (existing is not null)
            {
                pendingBytes = existing.FileData;
                pendingFileName = existing.FileName;
                pendingMime = existing.MimeType;
            }
        }
        if (pendingBytes.Length == 0)
        {
            FormStatus.Text = "Pilih gambar atau PDF terlebih dahulu.";
            return;
        }

        current.StudentId = student.Id;
        current.EvidenceDate = EvidenceDate.Date.DateTime;
        current.EvidenceType = type;
        current.Title = TitleBox.Text;
        current.FileName = pendingFileName;
        current.MimeType = pendingMime;
        current.FileData = pendingBytes;
        current.Notes = NotesBox.Text;
        DatabaseService.Instance.SaveEvidenceFile(current);
        FormStatus.Text = "Evidens berjaya disimpan dalam SQLite.";
        await PreviewBytesAsync(current.FileData, current.FileName, current.MimeType, current.Id);
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(current.Id)) return;
        DatabaseService.Instance.DeleteEvidenceFile(current.Id);
        New_Click(sender, e);
        FormStatus.Text = "Evidens dipadam (soft delete).";
        Refresh();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(current.Id) || App.MainWindowInstance is null)
        {
            FormStatus.Text = "Pilih satu rekod evidens dahulu.";
            return;
        }
        var full = DatabaseService.Instance.GetEvidenceFile(current.Id);
        if (full is null) return;
        var picker = PickerHelper.CreateSavePicker(App.MainWindowInstance);
        var ext = Path.GetExtension(full.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = full.MimeType == "application/pdf" ? ".pdf" : ".bin";
        picker.SuggestedFileName = Path.GetFileNameWithoutExtension(full.FileName);
        picker.FileTypeChoices.Add("Fail evidens", new List<string> { ext });
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await File.WriteAllBytesAsync(file.Path, full.FileData);
        FormStatus.Text = $"Fail dieksport: {file.Name}";
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private async void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not EvidenceFileRecord row) return;
        var full = DatabaseService.Instance.GetEvidenceFile(row.Id);
        if (full is null) return;
        current = full;
        pendingBytes = full.FileData;
        pendingFileName = full.FileName;
        pendingMime = full.MimeType;
        SelectStudent(full.StudentId);
        EvidenceDate.Date = new DateTimeOffset(full.EvidenceDate);
        EvidenceType.SelectedItem = full.EvidenceType;
        TitleBox.Text = full.Title;
        NotesBox.Text = full.Notes;
        SelectedFileText.Text = full.FileName;
        SelectedFileSizeText.Text = full.FileSizeText;
        FormStatus.Text = "Mod kemas kini.";
        await PreviewBytesAsync(full.FileData, full.FileName, full.MimeType, full.Id);
    }

    private void SelectStudent(string id)
    {
        if (StudentPicker.ItemsSource is not IEnumerable<Student> students) return;
        StudentPicker.SelectedItem = students.FirstOrDefault(x => x.Id == id);
    }

    private async Task PreviewBytesAsync(byte[] bytes, string fileName, string mime, string key)
    {
        ClearPreview();
        if (bytes.Length == 0) return;
        var dir = AppPaths.Ensure("evidence_preview");
        var safeExt = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(safeExt)) safeExt = mime == "application/pdf" ? ".pdf" : ".bin";
        var path = Path.Combine(dir, $"{key}{safeExt}");
        await File.WriteAllBytesAsync(path, bytes);

        if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            PreviewImage.Source = image;
            PreviewImage.Visibility = Visibility.Visible;
        }
        else if (mime == "application/pdf" || safeExt.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            PreviewWeb.Visibility = Visibility.Visible;
            PreviewWeb.Source = new Uri(path);
        }
        else
        {
            PreviewEmpty.Visibility = Visibility.Visible;
        }
    }

    private void ClearPreview()
    {
        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewWeb.Visibility = Visibility.Collapsed;
        PreviewEmpty.Visibility = Visibility.Visible;
    }

    private static string MimeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    private static string FormatSize(long size) => size switch
    {
        >= 1024L * 1024L => $"{size / 1024d / 1024d:0.0} MB",
        >= 1024L => $"{size / 1024d:0.0} KB",
        _ => $"{size} B"
    };
}
