namespace SistemPengurusanKehadiran.Models;
public sealed class Teacher
{
    public string Id { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Jawatan { get; set; } = "Guru";
    public string Telefon { get; set; } = "";
    public bool Aktif { get; set; } = true;
}
