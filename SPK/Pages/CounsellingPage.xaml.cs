using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using SistemPengurusanKehadiran.Models;using SistemPengurusanKehadiran.Services;
namespace SistemPengurusanKehadiran.Pages;
public sealed partial class CounsellingPage:Page
{
    private CounsellingSession current=new();private List<Student> students=[];private List<Teacher> teachers=[];
    public CounsellingPage(){InitializeComponent();Loaded+=(_,_)=>{students=DatabaseService.Instance.GetStudents();teachers=DatabaseService.Instance.GetTeachers().Where(x=>x.Aktif).ToList();StudentPicker.ItemsSource=students;TeacherPicker.ItemsSource=teachers;SessionType.ItemsSource=new[]{"INDIVIDU","KELOMPOK","IBU BAPA / PENJAGA","LAIN-LAIN"};NewRecord();Refresh();};}
    private void Refresh(){var x=DatabaseService.Instance.GetCounsellingSessions(Search?.Text??"");List.ItemsSource=x;CountText.Text=$"{x.Count} SESI";}
    private void NewRecord(){current=new CounsellingSession{SessionDate=DateTime.Today,SessionType="INDIVIDU"};StudentPicker.SelectedItem=null;TeacherPicker.SelectedItem=null;SessionDate.Date=DateTimeOffset.Now;SessionType.SelectedItem="INDIVIDU";Summary.Text=NextAction.Text=Notes.Text="";List.SelectedItem=null;FormTitle.Text="Sesi Baharu";FormStatus.Text="Rekod baharu.";}
    private void New_Click(object sender,RoutedEventArgs e)=>NewRecord();
    private void Save_Click(object sender,RoutedEventArgs e){if(StudentPicker.SelectedItem is not Student s){FormStatus.Text="Pilih murid terlebih dahulu.";return;}current.StudentId=s.Id;current.SessionDate=SessionDate.Date.DateTime;current.TeacherId=(TeacherPicker.SelectedItem as Teacher)?.Id??"";current.SessionType=SessionType.SelectedItem?.ToString()??"INDIVIDU";current.Summary=Summary.Text;current.NextAction=NextAction.Text;current.Notes=Notes.Text;DatabaseService.Instance.SaveCounsellingSession(current);FormStatus.Text="Sesi kaunseling berjaya disimpan.";Refresh();}
    private void Delete_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(current.Id))return;DatabaseService.Instance.DeleteCounsellingSession(current.Id);NewRecord();FormStatus.Text="Rekod dipadam (soft delete).";Refresh();}
    private void Search_TextChanged(object sender,TextChangedEventArgs e)=>Refresh();
    private void List_SelectionChanged(object sender,SelectionChangedEventArgs e){if(List.SelectedItem is not CounsellingSession x)return;current=x;StudentPicker.SelectedItem=students.FirstOrDefault(s=>s.Id==x.StudentId);TeacherPicker.SelectedItem=teachers.FirstOrDefault(t=>t.Id==x.TeacherId);SessionDate.Date=new DateTimeOffset(x.SessionDate);SessionType.SelectedItem=x.SessionType;Summary.Text=x.Summary;NextAction.Text=x.NextAction;Notes.Text=x.Notes;FormTitle.Text="Maklumat Sesi";FormStatus.Text="Mod kemas kini.";}
}
