using Microsoft.UI.Xaml;using Microsoft.UI.Xaml.Controls;using SistemPengurusanKehadiran.Models;using SistemPengurusanKehadiran.Services;
namespace SistemPengurusanKehadiran.Pages;
public sealed partial class GuardianContactsPage:Page
{
    private GuardianContact current=new(); private List<Student> students=[]; private bool loading;
    public GuardianContactsPage(){InitializeComponent();Loaded+=(_,_)=>{students=DatabaseService.Instance.GetStudents();StudentPicker.ItemsSource=students;Method.ItemsSource=new[]{"TELEFON","WHATSAPP","SMS","BERSEMUKA","SURAT","LAIN-LAIN"};NewRecord();Refresh();};}
    private void Refresh(){var x=DatabaseService.Instance.GetGuardianContacts(Search?.Text??"");List.ItemsSource=x;CountText.Text=$"{x.Count} REKOD";}
    private void NewRecord(){loading=true;current=new GuardianContact{ContactDate=DateTime.Today,Method="TELEFON"};StudentPicker.SelectedItem=null;ContactDate.Date=DateTimeOffset.Now;Method.SelectedItem="TELEFON";GuardianName.Text=Phone.Text=Outcome.Text=Notes.Text="";List.SelectedItem=null;FormTitle.Text="Rekod Hubungan Baharu";FormStatus.Text="Rekod baharu.";loading=false;}
    private void StudentPicker_SelectionChanged(object sender,SelectionChangedEventArgs e){if(loading||StudentPicker.SelectedItem is not Student s)return;if(string.IsNullOrWhiteSpace(current.Id)||s.Id!=current.StudentId){GuardianName.Text=s.Penjaga;Phone.Text=s.Telefon;}}
    private void New_Click(object sender,RoutedEventArgs e)=>NewRecord();
    private void Save_Click(object sender,RoutedEventArgs e){if(StudentPicker.SelectedItem is not Student s){FormStatus.Text="Pilih murid terlebih dahulu.";return;}current.StudentId=s.Id;current.ContactDate=ContactDate.Date.DateTime;current.Method=Method.SelectedItem?.ToString()??"TELEFON";current.GuardianName=GuardianName.Text;current.Phone=Phone.Text;current.Outcome=Outcome.Text;current.Notes=Notes.Text;DatabaseService.Instance.SaveGuardianContact(current);FormStatus.Text="Hubungan penjaga berjaya disimpan.";Refresh();}
    private void Delete_Click(object sender,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(current.Id))return;DatabaseService.Instance.DeleteGuardianContact(current.Id);NewRecord();FormStatus.Text="Rekod dipadam (soft delete).";Refresh();}
    private void Search_TextChanged(object sender,TextChangedEventArgs e)=>Refresh();
    private void List_SelectionChanged(object sender,SelectionChangedEventArgs e){if(List.SelectedItem is not GuardianContact x)return;loading=true;current=x;StudentPicker.SelectedItem=students.FirstOrDefault(s=>s.Id==x.StudentId);ContactDate.Date=new DateTimeOffset(x.ContactDate);Method.SelectedItem=x.Method;GuardianName.Text=x.GuardianName;Phone.Text=x.Phone;Outcome.Text=x.Outcome;Notes.Text=x.Notes;FormTitle.Text="Maklumat Hubungan";FormStatus.Text="Mod kemas kini.";loading=false;}
}
