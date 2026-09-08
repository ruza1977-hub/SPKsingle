namespace SistemPengurusanKehadiran.Models;
public sealed class AttendanceRow
{
    public string StudentId { get; set; } = "";
    public string NoMurid { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Kelas { get; set; } = "";
    public string Status { get; set; } = "HADIR";
    public string Kategori { get; set; } = "";
    public string Sebab { get; set; } = "";
    public string Catatan { get; set; } = "";
}
