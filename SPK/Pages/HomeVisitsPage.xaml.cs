using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using SistemPengurusanKehadiran.Models;using SistemPengurusanKehadiran.Services;
namespace SistemPengurusanKehadiran.Pages;
public sealed partial class HomeVisitsPage:Page
{
    private HomeVisit current=new();private List<Student> students=[];private List<Teacher> teachers=[];private bool loading;
    public HomeVisitsPage(){InitializeComponent();Loaded+=(_,_)=>{students=DatabaseService.Instance.GetStudents();teachers=DatabaseService.Instance.GetTeachers().Where(x=>x.Aktif).ToList();StudentPicker.ItemsSource=students;TeacherPicker.ItemsSource=teachers;NewRecord();Refresh();};}
    private void Refresh(){var x=DatabaseService.Instance.GetHomeVisits(Search?.Text??"");List.ItemsSource=x;CountText.Text=$"{x.Count} LAWATAN";}
    private void NewRecord(){loading=true;current=new HomeVisit{VisitDate=DateTime.Today};StudentPicker.SelectedItem=null;TeacherPicker.SelectedItem=null;VisitDate.Date=DateTimeOffset.Now;Address.Text=Outcome.Text=FollowUp.Text=Notes.Text="";List.SelectedItem=null;FormTitle.Text="Lawatan Baharu";FormStatus.Text="Rekod baharu.";loading=false;}
    private void StudentPicker_SelectionChanged(object sender,SelectionChangedEventArgs e){if(loading||!string.IsNullOrWhiteSpace(current.Id)||StudentPicker.SelectedItem is not Student s)return;if(string.IsNullOrWhiteSpace(Address.Text))Address.Text=s.Alamat;}
    private void New_Click(object sender,RoutedEventArgs e)=>NewRecord();
    private void Save_Click(object sender,RoutedEventArgs e){if(StudentPicker.SelectedItem is not Student s){FormStatus.Text="Pilih murid terlebih dahulu.";return;}current.StudentId=s.Id;current.VisitDate=VisitDate.Date.DateTime;current.TeacherId=(TeacherPicker.SelectedItem as Teacher)?.Id??"";current.Address=Address.Text;current.Outcome=Outcome.Text;current.FollowUp=FollowUp.Text;current.Notes=Notes.Text;DatabaseService.Instance.SaveHomeVisit(current);FormStatus.Text="Lawatan rumah berjaya disimpan.";Refresh();}
    private void Delete_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(current.Id))return;DatabaseService.Instance.DeleteHomeVisit(current.Id);NewRecord();FormStatus.Text="Rekod dipadam (soft delete).";Refresh();}
    private void Search_TextChanged(object sender,TextChangedEventArgs e)=>Refresh();
    private void List_SelectionChanged(object sender,SelectionChangedEventArgs e){if(List.SelectedItem is not HomeVisit x)return;loading=true;current=x;StudentPicker.SelectedItem=students.FirstOrDefault(s=>s.Id==x.StudentId);TeacherPicker.SelectedItem=teachers.FirstOrDefault(t=>t.Id==x.TeacherId);VisitDate.Date=new DateTimeOffset(x.VisitDate);Address.Text=x.Address;Outcome.Text=x.Outcome;FollowUp.Text=x.FollowUp;Notes.Text=x.Notes;FormTitle.Text="Maklumat Lawatan";FormStatus.Text="Mod kemas kini.";loading=false;}
}
