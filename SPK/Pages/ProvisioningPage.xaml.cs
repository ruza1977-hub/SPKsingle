using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class ProvisioningPage : Page
{
    public ProvisioningPage()
    {
        InitializeComponent();
        DeviceText.Text = $"Peranti: {Environment.MachineName}  •  Windows  •  {ShortId(ProvisioningService.Instance.DeviceId)}";
    }

    private void ActivationCodeBox_TextChanged(object sender, TextChangedEventArgs e)
        => ActivateButton.IsEnabled = ActivationCodeBox.Text.Trim().Length >= 6;

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        ActivateButton.IsEnabled = false;
        Busy.IsActive = true;
        Busy.Visibility = Visibility.Visible;
        StatusBar.IsOpen = false;
        try
        {
            var result = await ProvisioningService.Instance.ActivateAsync(ActivationCodeBox.Text);
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title = "Peranti berjaya disahkan";
            StatusBar.Message = $"ID Guru: {result.TeacherId ?? "-"}. Sedang menyelaraskan data sekolah...";
            StatusBar.IsOpen = true;

            ActivationCodeBox.Text = "";
            try { await AutoSyncService.Instance.TriggerNowAsync(); } catch { }
            await Task.Delay(500);
            App.MainWindowInstance?.ShowDashboardAfterProvisioning();
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Title = "Pengaktifan gagal";
            StatusBar.Message = ex.Message;
            StatusBar.IsOpen = true;
        }
        finally
        {
            Busy.IsActive = false;
            Busy.Visibility = Visibility.Collapsed;
            ActivateButton.IsEnabled = ActivationCodeBox.Text.Trim().Length >= 6;
        }
    }

    private static string ShortId(string value) => value.Length <= 12 ? value : value[..12] + "…";
}
