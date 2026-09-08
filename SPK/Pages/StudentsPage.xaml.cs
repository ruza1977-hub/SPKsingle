using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;

public sealed partial class StudentsPage : Page
{
    private Student current = new();

    public StudentsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var items = DatabaseService.Instance.GetStudents(Search?.Text ?? "");
        List.ItemsSource = items;
        CountText.Text = $"{items.Count} MURID";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        current = new Student { Aktif = true };
        NoMurid.Text = Nama.Text = Kelas.Text = Tingkatan.Text = Penjaga.Text = Telefon.Text = Alamat.Text = "";
        Aktif.IsOn = true;
        List.SelectedItem = null;
        FormStatus.Text = "Rekod baharu.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NoMurid.Text) || string.IsNullOrWhiteSpace(Nama.Text) || string.IsNullOrWhiteSpace(Kelas.Text))
        {
            FormStatus.Text = "No. Murid, Nama dan Kelas mesti diisi.";
            return;
        }

        current.NoMurid = NoMurid.Text;
        current.Nama = Nama.Text;
        current.Kelas = Kelas.Text;
        current.Tingkatan = Tingkatan.Text;
        current.Penjaga = Penjaga.Text;
        current.Telefon = Telefon.Text;
        current.Alamat = Alamat.Text;
        current.Aktif = Aktif.IsOn;
        DatabaseService.Instance.SaveStudent(current);
        FormStatus.Text = "Rekod murid berjaya disimpan.";
        current = new Student { Aktif = true };
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(current.Id)) return;
        DatabaseService.Instance.DeleteStudent(current.Id);
        New_Click(sender, e);
        FormStatus.Text = "Rekod murid dipadam (soft delete).";
        Refresh();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is not Student x) return;
        current = x;
        NoMurid.Text = x.NoMurid;
        Nama.Text = x.Nama;
        Kelas.Text = x.Kelas;
        Tingkatan.Text = x.Tingkatan;
        Penjaga.Text = x.Penjaga;
        Telefon.Text = x.Telefon;
        Alamat.Text = x.Alamat;
        Aktif.IsOn = x.Aktif;
        FormStatus.Text = "Mod kemas kini.";
    }
}
