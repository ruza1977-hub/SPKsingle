namespace SistemPengurusanKehadiran.Models;

public sealed class WarningLetter
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime IssueDate { get; set; } = DateTime.Today;
    public string WarningType { get; set; } = "AMARAN 1";
    public string ReferenceNo { get; set; } = "";
    public string GuardianName { get; set; } = "";
    public string GuardianAddress { get; set; } = "";
    public int TotalAbsences { get; set; }
    public string Title { get; set; } = "SURAT AMARAN 1: KETIDAKHADIRAN KE SEKOLAH";
    public string Body { get; set; } = "";
    public string Notes { get; set; } = "";
    public byte[]? PdfData { get; set; }
    public string SyncStatus { get; set; } = "pending";
    public string DateText => IssueDate.ToString("dd/MM/yyyy");
    public string StudentDisplay => $"{StudentName} · {Kelas}";
}
