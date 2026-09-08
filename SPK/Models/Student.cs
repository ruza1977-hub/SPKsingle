namespace SistemPengurusanKehadiran.Models;
public sealed class Student
{
    public string Id { get; set; } = "";
    public string SchoolId { get; set; } = "";
    public string NoMurid { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Kelas { get; set; } = "";
    public string Tingkatan { get; set; } = "";
    public string Penjaga { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string Alamat { get; set; } = "";
    public bool Aktif { get; set; } = true;
    public string Display => $"{Nama} · {Kelas}";
}
