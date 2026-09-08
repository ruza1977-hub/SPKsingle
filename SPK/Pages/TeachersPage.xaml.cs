using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class TeachersPage : Page
{
    private Teacher current = new();

    public TeachersPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var query = (Search?.Text ?? "").Trim();
        var items = DatabaseService.Instance.GetTeachers()
            .Where(x => string.IsNullOrWhiteSpace(query) || $"{x.Nama} {x.Jawatan} {x.Telefon}".Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        List.ItemsSource = items;
        CountText.Text = $"{items.Count} GURU";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        current = new Teacher { Aktif = true };
        Nama.Text = "";
        Jawatan.Text = "Guru";
        Telefon.Text = "";
        Aktif.IsOn = true;
        List.SelectedItem = null;
        FormStatus.Text = "Rekod baharu.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Nama.Text))
        {
            FormStatus.Text = "Nama guru mesti diisi.";
            return;
        }
        current.Nama = Nama.Text;
        current.Jawatan = Jawatan.Text;
        current.Telefon = Telefon.Text;
        current.Aktif = Aktif.IsOn;
        DatabaseService.Instance.SaveTeacher(current);
        FormStatus.Text = "Rekod guru berjaya disimpan.";
        current = new Teacher { Aktif = true };
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(current.Id)) return;
        DatabaseService.Instance.DeleteTeacher(current.Id);
        New_Click(sender, e);
        FormStatus.Text = "Rekod guru dipadam (soft delete).";
        Refresh();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not Teacher x) return;
        current = x;
        Nama.Text = x.Nama;
        Jawatan.Text = x.Jawatan;
        Telefon.Text = x.Telefon;
        Aktif.IsOn = x.Aktif;
        FormStatus.Text = "Mod kemas kini.";
    }
}
