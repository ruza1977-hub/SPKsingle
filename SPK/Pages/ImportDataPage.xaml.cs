using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Helpers;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class ImportDataPage : Page
{
    public ImportDataPage() => InitializeComponent();

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = PickerHelper.CreateOpenPicker(App.MainWindowInstance!);
        picker.FileTypeFilter.Add(".zip");
        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        Status.Text = "Memproses ZIP...";
        try
        {
            var summary = UniversalZipImportService.Instance.Import(file.Path);
            Status.Text = summary.NeedsReview > 0
                ? $"Import berjaya • {summary}\nPakej mempunyai {summary.NeedsReview} item yang ditanda untuk semakan."
                : $"Import berjaya • {summary}";
        }
        catch (Exception ex)
        {
            Status.Text = "Import gagal: " + ex.Message;
        }
    }
}
