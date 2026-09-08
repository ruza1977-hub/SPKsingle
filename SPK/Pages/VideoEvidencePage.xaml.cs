using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Helpers;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;
using Windows.Media.Core;
using Windows.Storage;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class VideoEvidencePage : Page
{
    private VideoEvidenceRecord current = new();
    private readonly DispatcherTimer autosaveTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private bool loadingForm;
    private RightPanelFocus rightPanelFocus = RightPanelFocus.Normal;

    private enum RightPanelFocus
    {
        Normal,
        VideoList,
        VideoForm
    }

    public VideoEvidencePage()
    {
        InitializeComponent();
        autosaveTimer.Tick += (_, _) =>
        {
            autosaveTimer.Stop();
            if (!loadingForm && !string.IsNullOrWhiteSpace(current.Id)) PersistCurrent(true);
        };
        Loaded += (_, _) =>
        {
            StudentPicker.ItemsSource = DatabaseService.Instance.GetStudents();
            EvidenceDate.Date = DateTimeOffset.Now;
            Refresh();
        };
    }

    private void Refresh()
    {
        var items = DatabaseService.Instance.GetVideoEvidence(SearchBox?.Text ?? "");
        List.ItemsSource = items;
        CountText.Text = items.Count.ToString();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        loadingForm = true;
        current = new VideoEvidenceRecord();
        StudentPicker.SelectedItem = null;
        EvidenceDate.Date = DateTimeOffset.Now;
        TitleBox.Text = UrlBox.Text = NotesBox.Text = "";
        ProviderText.Text = "Belum dikesan";
        SourceIdText.Text = "";
        CacheStatus.Text = "";
        List.SelectedItem = null;
        ClearPreview();
        FormStatus.Text = "Rekod video baharu.";
        loadingForm = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (PersistCurrent(false))
        {
            FormStatus.Text = "Rekod video berjaya disimpan.";
            Refresh();
        }
    }

    private bool PersistCurrent(bool quiet)
    {
        if (StudentPicker.SelectedItem is not Student student)
        {
            if (!quiet) FormStatus.Text = "Pilih murid terlebih dahulu.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(TitleBox.Text) && string.IsNullOrWhiteSpace(UrlBox.Text))
        {
            if (!quiet) FormStatus.Text = "Masukkan tajuk atau pautan video.";
            return false;
        }

        var parsed = VideoLinkHelper.Parse(UrlBox.Text);
        current.StudentId = student.Id;
        current.EvidenceDate = EvidenceDate.Date.DateTime;
        current.Provider = parsed.Provider;
        current.SourceUrl = UrlBox.Text.Trim();
        current.SourceId = parsed.SourceId;
        current.Title = TitleBox.Text.Trim();
        current.Notes = NotesBox.Text.Trim();
        DatabaseService.Instance.SaveVideoEvidence(current);
        UpdateProvider(parsed);
        UpdateCacheStatus();
        if (quiet) FormStatus.Text = $"AutoSave • {DateTime.Now:HH:mm:ss}";
        return true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(current.Id)) return;
        DatabaseService.Instance.DeleteVideoEvidence(current.Id);
        DeleteCacheFiles(current.Id);
        New_Click(sender, e);
        FormStatus.Text = "Rekod video dipadam (soft delete).";
        Refresh();
    }

    private async void Preview_Click(object sender, RoutedEventArgs e) => await PreviewCurrentAsync();

    private async Task PreviewCurrentAsync()
    {
        ClearPreview();
        var cached = FindCacheFile(current.Id);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(cached);
                LocalPlayer.Source = MediaSource.CreateFromStorageFile(file);
                LocalPlayer.Visibility = Visibility.Visible;
                PreviewEmpty.Visibility = Visibility.Collapsed;
                CacheStatus.Text = $"Cache lokal aktif: {Path.GetFileName(cached)}";
                return;
            }
            catch (Exception ex)
            {
                CacheStatus.Text = $"Cache tidak dapat dimainkan: {ex.Message}";
            }
        }

        var parsed = VideoLinkHelper.Parse(UrlBox.Text);
        UpdateProvider(parsed);
        if (string.IsNullOrWhiteSpace(parsed.PreviewUrl))
        {
            PreviewEmpty.Visibility = Visibility.Visible;
            FormStatus.Text = "Pautan video tidak dapat dikenal pasti untuk pratonton.";
            return;
        }

        try
        {
            VideoWeb.MaxWidth = parsed.IsVertical ? 430 : double.PositiveInfinity;
            VideoWeb.HorizontalAlignment = parsed.IsVertical ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            VideoWeb.Visibility = Visibility.Visible;
            PreviewEmpty.Visibility = Visibility.Collapsed;
            VideoWeb.Source = new Uri(parsed.PreviewUrl);
        }
        catch (Exception ex)
        {
            PreviewEmpty.Visibility = Visibility.Visible;
            FormStatus.Text = $"Pratonton gagal: {ex.Message}";
        }
    }

    private async void CacheVideo_Click(object sender, RoutedEventArgs e)
    {
        if (!PersistCurrent(false)) return;
        if (App.MainWindowInstance is null) return;
        var picker = PickerHelper.CreateOpenPicker(App.MainWindowInstance);
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".mov");
        picker.FileTypeFilter.Add(".m4v");
        picker.FileTypeFilter.Add(".webm");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        DeleteCacheFiles(current.Id);
        var ext = string.IsNullOrWhiteSpace(file.FileType) ? ".mp4" : file.FileType.ToLowerInvariant();
        var destination = Path.Combine(AppPaths.Cache, current.Id + ext);
        File.Copy(file.Path, destination, true);
        CacheStatus.Text = $"Video lokal dicache: {file.Name}";
        FormStatus.Text = "Cache video lokal berjaya disimpan.";
        await PreviewCurrentAsync();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void SearchFocus_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void StopVideo_Click(object sender, RoutedEventArgs e)
    {
        try { LocalPlayer.MediaPlayer?.Pause(); } catch { }
        try { VideoWeb.CoreWebView2?.Stop(); } catch { }
        FormStatus.Text = "Video dihentikan.";
    }

    private async void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not VideoEvidenceRecord row) return;
        loadingForm = true;
        current = row;
        SelectStudent(row.StudentId);
        EvidenceDate.Date = new DateTimeOffset(row.EvidenceDate);
        TitleBox.Text = row.Title;
        UrlBox.Text = row.SourceUrl;
        NotesBox.Text = row.Notes;
        UpdateProvider(VideoLinkHelper.Parse(row.SourceUrl));
        UpdateCacheStatus();
        FormStatus.Text = "Mod kemas kini.";
        loadingForm = false;
        await PreviewCurrentAsync();
    }

    private void Url_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateProvider(VideoLinkHelper.Parse(UrlBox.Text));
        ScheduleAutosave();
    }

    private void FormTextChanged(object sender, TextChangedEventArgs e) => ScheduleAutosave();
    private void FormChanged(object sender, SelectionChangedEventArgs e) => ScheduleAutosave();
    private void DateChanged(object sender, DatePickerValueChangedEventArgs args) => ScheduleAutosave();

    private void ScheduleAutosave()
    {
        if (loadingForm || string.IsNullOrWhiteSpace(current.Id)) return;
        autosaveTimer.Stop();
        autosaveTimer.Start();
    }

    private void SelectStudent(string id)
    {
        if (StudentPicker.ItemsSource is not IEnumerable<Student> students) return;
        StudentPicker.SelectedItem = students.FirstOrDefault(x => x.Id == id);
    }

    private void UpdateProvider((string Provider, string SourceId, string PreviewUrl, bool IsVertical) parsed)
    {
        ProviderText.Text = parsed.Provider switch
        {
            "YOUTUBE" => parsed.IsVertical ? "YouTube Shorts" : "YouTube",
            "GOOGLE_DRIVE" => "Google Drive",
            "URL" => "Pautan Video",
            _ => "Belum dikesan"
        };
        SourceIdText.Text = string.IsNullOrWhiteSpace(parsed.SourceId) ? "" : parsed.SourceId;
    }

    private void UpdateCacheStatus()
    {
        var cached = FindCacheFile(current.Id);
        CacheStatus.Text = string.IsNullOrWhiteSpace(cached)
            ? "Tiada cache video lokal. Pautan asal kekal sebagai metadata."
            : $"Cache lokal tersedia: {Path.GetFileName(cached)}";
    }

    private static string? FindCacheFile(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Directory.Exists(AppPaths.Cache)) return null;
        return Directory.GetFiles(AppPaths.Cache, id + ".*").FirstOrDefault();
    }

    private static void DeleteCacheFiles(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Directory.Exists(AppPaths.Cache)) return;
        foreach (var file in Directory.GetFiles(AppPaths.Cache, id + ".*"))
        {
            try { File.Delete(file); } catch { }
        }
    }


    private void ToggleVideoListPanel_Click(object sender, RoutedEventArgs e)
    {
        SetRightPanelFocus(rightPanelFocus == RightPanelFocus.VideoList
            ? RightPanelFocus.Normal
            : RightPanelFocus.VideoList);
    }

    private void ToggleVideoFormPanel_Click(object sender, RoutedEventArgs e)
    {
        SetRightPanelFocus(rightPanelFocus == RightPanelFocus.VideoForm
            ? RightPanelFocus.Normal
            : RightPanelFocus.VideoForm);
    }

    private void SetRightPanelFocus(RightPanelFocus focus)
    {
        rightPanelFocus = focus;

        if (focus == RightPanelFocus.VideoList)
        {
            VideoListRow.Height = new GridLength(1, GridUnitType.Star);
            VideoFormRow.Height = GridLength.Auto;
            VideoListBody.Visibility = Visibility.Visible;
            VideoFormBody.Visibility = Visibility.Collapsed;
            ListPanelButton.Content = "Pulih";
            FormPanelButton.Content = "Besarkan";
            return;
        }

        if (focus == RightPanelFocus.VideoForm)
        {
            VideoListRow.Height = GridLength.Auto;
            VideoFormRow.Height = new GridLength(1, GridUnitType.Star);
            VideoListBody.Visibility = Visibility.Collapsed;
            VideoFormBody.Visibility = Visibility.Visible;
            ListPanelButton.Content = "Besarkan";
            FormPanelButton.Content = "Pulih";
            return;
        }

        VideoListRow.Height = new GridLength(330);
        VideoFormRow.Height = new GridLength(1, GridUnitType.Star);
        VideoListBody.Visibility = Visibility.Visible;
        VideoFormBody.Visibility = Visibility.Visible;
        ListPanelButton.Content = "Besarkan";
        FormPanelButton.Content = "Besarkan";
    }

    private void ClearPreview()
    {
        try { LocalPlayer.MediaPlayer?.Pause(); } catch { }
        LocalPlayer.Source = null;
        LocalPlayer.Visibility = Visibility.Collapsed;
        VideoWeb.Visibility = Visibility.Collapsed;
        PreviewEmpty.Visibility = Visibility.Visible;
    }
}
