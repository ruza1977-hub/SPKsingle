using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SistemPengurusanKehadiran.Models;
using SistemPengurusanKehadiran.Services;

namespace SistemPengurusanKehadiran.Pages;
public sealed partial class CaseProfilesPage : Page
{
    private CaseProfile current=new();
    private List<Student> students=[]; private List<Teacher> teachers=[];
    public CaseProfilesPage(){InitializeComponent();Loaded+=(_,_)=>{LoadPickers();NewRecord();Refresh();};}
    private void LoadPickers(){students=DatabaseService.Instance.GetStudents();teachers=DatabaseService.Instance.GetTeachers().Where(x=>x.Aktif).ToList();StudentPicker.ItemsSource=students;TeacherPicker.ItemsSource=teachers;Status.ItemsSource=new[]{"AKTIF","PEMANTAUAN","SELESAI"};Category.ItemsSource=new[]{"PONTENG","TIDAK HADIR BERULANG","LEWAT","LAIN-LAIN"};}
    private void Refresh(){var items=DatabaseService.Instance.GetCaseProfiles(Search?.Text??"");List.ItemsSource=items;CountText.Text=$"{items.Count} KES";}
    private void NewRecord(){current=new CaseProfile{OpenedAt=DateTime.Today,Status="AKTIF",Category="PONTENG"};StudentPicker.SelectedItem=null;TeacherPicker.SelectedItem=null;OpenedAt.Date=DateTimeOffset.Now;Status.SelectedItem="AKTIF";Category.SelectedItem="PONTENG";Summary.Text=Notes.Text="";List.SelectedItem=null;FormTitle.Text="Buka Kes Baharu";FormStatus.Text="Rekod baharu.";}
    private void New_Click(object sender,RoutedEventArgs e)=>NewRecord();
    private void Save_Click(object sender,RoutedEventArgs e){if(StudentPicker.SelectedItem is not Student s){FormStatus.Text="Pilih murid terlebih dahulu.";return;}current.StudentId=s.Id;current.OpenedAt=OpenedAt.Date.DateTime;current.Status=Status.SelectedItem?.ToString()??"AKTIF";current.Category=Category.SelectedItem?.ToString()??"PONTENG";current.TeacherId=(TeacherPicker.SelectedItem as Teacher)?.Id??"";current.Summary=Summary.Text;current.Notes=Notes.Text;DatabaseService.Instance.SaveCaseProfile(current);FormStatus.Text="Profil kes berjaya disimpan.";Refresh();}
    private void Delete_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(current.Id))return;DatabaseService.Instance.DeleteCaseProfile(current.Id);NewRecord();FormStatus.Text="Profil kes dipadam (soft delete).";Refresh();}
    private void Search_TextChanged(object sender,TextChangedEventArgs e)=>Refresh();
    private void List_SelectionChanged(object sender,SelectionChangedEventArgs e){if(List.SelectedItem is not CaseProfile x)return;current=x;StudentPicker.SelectedItem=students.FirstOrDefault(s=>s.Id==x.StudentId);TeacherPicker.SelectedItem=teachers.FirstOrDefault(t=>t.Id==x.TeacherId);OpenedAt.Date=new DateTimeOffset(x.OpenedAt);Status.SelectedItem=x.Status;Category.SelectedItem=x.Category;Summary.Text=x.Summary;Notes.Text=x.Notes;FormTitle.Text="Maklumat Kes";FormStatus.Text="Mod kemas kini.";}
}
