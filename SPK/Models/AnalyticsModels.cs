namespace SistemPengurusanKehadiran.Models;

public sealed class AnalyticsReportData
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ClassFilter { get; set; } = "SEMUA KELAS";
    public int TotalRecords { get; set; }
    public int PresentRecords { get; set; }
    public int NotPresentRecords { get; set; }
    public int PontengRecords { get; set; }
    public int ActiveCases { get; set; }
    public int RecoveredCases { get; set; }
    public double AttendanceRate => TotalRecords == 0 ? 0 : (double)PresentRecords / TotalRecords * 100.0;
    public List<AnalyticsTrendRow> Trend { get; set; } = new();
    public List<AnalyticsClassRow> Classes { get; set; } = new();
    public List<AnalyticsRiskRow> RiskStudents { get; set; } = new();
}

public sealed class AnalyticsTrendRow
{
    public int Month { get; set; }
    public string MonthLabel { get; set; } = "";
    public int Total { get; set; }
    public int Present { get; set; }
    public double AttendanceRate => Total == 0 ? 0 : (double)Present / Total * 100.0;
    public string AttendanceRateText => $"{AttendanceRate:0.0}%";
}

public sealed class AnalyticsClassRow
{
    public string Kelas { get; set; } = "";
    public int TidakHadir { get; set; }
    public int Ponteng { get; set; }
    public int Total { get; set; }
    public int Present { get; set; }
    public double AttendanceRate => Total == 0 ? 0 : (double)Present / Total * 100.0;
    public string AttendanceRateText => Total == 0 ? "—" : $"{AttendanceRate:0.0}%";
}

public sealed class AnalyticsRiskRow
{
    public string StudentId { get; set; } = "";
    public string Nama { get; set; } = "";
    public string Kelas { get; set; } = "";
    public int TidakHadir { get; set; }
    public int Ponteng { get; set; }
    public int Lewat { get; set; }
    public int Total { get; set; }
    public int Present { get; set; }
    public int ActiveCaseCount { get; set; }
    public int RiskScore => Ponteng * 4 + TidakHadir * 2 + Lewat;
    public double AttendanceRate => Total == 0 ? 0 : (double)Present / Total * 100.0;
    public string AttendanceRateText => Total == 0 ? "—" : $"{AttendanceRate:0.0}%";
    public string RiskText => RiskScore >= 12 ? "TINGGI" : RiskScore >= 6 ? "SEDERHANA" : "RENDAH";
    public string CaseText => ActiveCaseCount > 0 ? $"{ActiveCaseCount} AKTIF" : "—";
}
