namespace SistemPengurusanKehadiran.Models;

public sealed class ReportDocument
{
    public string Title { get; set; } = "";
    public string SchoolName { get; set; } = "";
    public string PeriodText { get; set; } = "";
    public string ClassText { get; set; } = "SEMUA KELAS";
    public List<string> Summary { get; set; } = new();
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}

public sealed class AttendanceReportRow
{
    public DateTime Date { get; set; }
    public string StudentId { get; set; } = "";
    public string NoMurid { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Kelas { get; set; } = "";
    public string Status { get; set; } = "";
    public string Kategori { get; set; } = "";
    public string Sebab { get; set; } = "";
    public string Catatan { get; set; } = "";
}
