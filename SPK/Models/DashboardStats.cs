namespace SistemPengurusanKehadiran.Models;
public sealed class DashboardStats
{
    public int ActiveStudents { get; set; }
    public int ActiveTeachers { get; set; }
    public int SchoolDaysThisYear { get; set; }
    public int HadirToday { get; set; }
    public int TidakHadirToday { get; set; }
    public int ActiveCases { get; set; }
    public int RecoveredCases { get; set; }
    public int PendingSync { get; set; }
}
