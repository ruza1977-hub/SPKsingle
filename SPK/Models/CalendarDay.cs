namespace SistemPengurusanKehadiran.Models;
public sealed class CalendarDay
{
    public string Id { get; set; } = "";
    public DateTime Tarikh { get; set; } = DateTime.Today;
    public string Jenis { get; set; } = "Hari Persekolahan";
    public string Tajuk { get; set; } = "";
    public bool HariPersekolahan { get; set; } = true;
    public int? MingguAkademik { get; set; }
    public string Catatan { get; set; } = "";
}
