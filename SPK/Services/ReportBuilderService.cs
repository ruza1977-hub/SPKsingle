using SistemPengurusanKehadiran.Models;
using System.Globalization;

namespace SistemPengurusanKehadiran.Services;

public static class ReportBuilderService
{
    public static readonly string[] ReportTypes =
    {
        "Ringkasan & Analitik Kehadiran",
        "Kehadiran Terperinci",
        "Murid Tidak Hadir / Ponteng",
        "Murid Berisiko",
        "Kes Aktif / Pemantauan",
        "Kes Dipulihkan",
        "Hubungan Penjaga",
        "Lawatan Rumah",
        "Sesi Kaunseling",
        "Intervensi",
        "Eviden",
        "Senarai Murid",
        "Senarai Guru"
    };

    public static string Description(string type) => type switch
    {
        "Ringkasan & Analitik Kehadiran" => "Ringkasan KPI, kadar hadir, prestasi kelas dan trend tempoh pilihan.",
        "Kehadiran Terperinci" => "Senarai setiap rekod kehadiran mengikut tarikh, murid, kelas dan status.",
        "Murid Tidak Hadir / Ponteng" => "Senarai ketidakhadiran, ponteng, MC dan cuti bersebab untuk tindakan susulan.",
        "Murid Berisiko" => "Murid disusun mengikut skor risiko berdasarkan ponteng, tidak hadir dan lewat.",
        "Kes Aktif / Pemantauan" => "Profil kes yang masih aktif atau sedang dalam pemantauan.",
        "Kes Dipulihkan" => "Kes yang telah selesai/dipulihkan berserta hasil pemulihan.",
        "Hubungan Penjaga" => "Rekod komunikasi dengan penjaga dalam tempoh pilihan.",
        "Lawatan Rumah" => "Rekod lawatan rumah, dapatan dan tindakan susulan.",
        "Sesi Kaunseling" => "Rekod sesi kaunseling murid dan tindakan seterusnya.",
        "Intervensi" => "Pelan intervensi, guru bertanggungjawab, sasaran dan status.",
        "Eviden" => "Senarai eviden gambar/dokumen yang direkodkan dalam sistem.",
        "Senarai Murid" => "Direktori murid aktif mengikut kelas.",
        "Senarai Guru" => "Direktori guru aktif sekolah.",
        _ => "Laporan SPK."
    };

    public static ReportDocument Build(string type, DateTime start, DateTime end, string kelas)
    {
        var db = DatabaseService.Instance;
        var school = db.GetSchoolProfile();
        var report = new ReportDocument
        {
            Title = type.ToUpperInvariant(),
            SchoolName = school.Name,
            PeriodText = FormatPeriod(start, end),
            ClassText = kelas
        };

        switch (type)
        {
            case "Ringkasan & Analitik Kehadiran": BuildAnalytics(report, db.GetAnalyticsReport(start, end, kelas)); break;
            case "Kehadiran Terperinci": BuildAttendance(report, db.GetAttendanceReportRows(start, end, kelas), false); break;
            case "Murid Tidak Hadir / Ponteng": BuildAttendance(report, db.GetAttendanceReportRows(start, end, kelas), true); break;
            case "Murid Berisiko": BuildRisk(report, db.GetAnalyticsReport(start, end, kelas)); break;
            case "Kes Aktif / Pemantauan": BuildCases(report, db.GetCaseProfiles().Where(x => x.OpenedAt >= start && x.OpenedAt <= end && ClassOk(x.Kelas, kelas) && x.Status != "SELESAI")); break;
            case "Kes Dipulihkan": BuildRecovered(report, db.GetCaseProfiles().Where(x => x.Status == "SELESAI" && x.RecoveredAt.HasValue && x.RecoveredAt.Value >= start && x.RecoveredAt.Value <= end && ClassOk(x.Kelas, kelas))); break;
            case "Hubungan Penjaga": BuildGuardian(report, db.GetGuardianContacts().Where(x => x.ContactDate >= start && x.ContactDate <= end && ClassOk(x.Kelas, kelas))); break;
            case "Lawatan Rumah": BuildVisits(report, db.GetHomeVisits().Where(x => x.VisitDate >= start && x.VisitDate <= end && ClassOk(x.Kelas, kelas))); break;
            case "Sesi Kaunseling": BuildCounselling(report, db.GetCounsellingSessions().Where(x => x.SessionDate >= start && x.SessionDate <= end && ClassOk(x.Kelas, kelas))); break;
            case "Intervensi": BuildInterventions(report, db.GetInterventions().Where(x => x.InterventionDate >= start && x.InterventionDate <= end && ClassOk(x.Kelas, kelas))); break;
            case "Eviden": BuildEvidence(report, db.GetEvidenceFiles().Where(x => x.EvidenceDate >= start && x.EvidenceDate <= end && ClassOk(x.Kelas, kelas))); break;
            case "Senarai Murid": BuildStudents(report, db.GetStudents().Where(x => x.Aktif && ClassOk(x.Kelas, kelas))); break;
            case "Senarai Guru": BuildTeachers(report, db.GetTeachers().Where(x => x.Aktif)); break;
            default: BuildAnalytics(report, db.GetAnalyticsReport(start, end, kelas)); break;
        }
        report.Summary.Insert(0, $"Jumlah baris: {report.Rows.Count}");
        return report;
    }

    private static bool ClassOk(string value, string filter) => filter == "SEMUA KELAS" || string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
    private static string D(DateTime d) => d.ToString("dd/MM/yyyy");
    private static string N(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

    private static string FormatPeriod(DateTime start, DateTime end)
    {
        var ms = new CultureInfo("ms-MY");
        return start.Month == 1 && start.Day == 1 && end.Month == 12 && end.Day == 31
            ? $"Tahun {start.Year}"
            : $"{start.ToString("dd MMM yyyy", ms)} – {end.ToString("dd MMM yyyy", ms)}";
    }

    private static void BuildAnalytics(ReportDocument r, AnalyticsReportData x)
    {
        r.Summary.Add($"Kadar hadir: {x.AttendanceRate:0.0}%");
        r.Summary.Add($"Tidak hadir: {x.NotPresentRecords} | Ponteng: {x.PontengRecords} | Kes aktif: {x.ActiveCases} | Dipulihkan: {x.RecoveredCases}");
        r.Headers = new() { "Kelas", "Tidak Hadir", "Ponteng", "Jumlah Rekod", "% Hadir" };
        r.Rows = x.Classes.Select(a => new List<string> { a.Kelas, a.TidakHadir.ToString(), a.Ponteng.ToString(), a.Total.ToString(), a.AttendanceRateText }).ToList();
    }

    private static void BuildAttendance(ReportDocument r, IEnumerable<AttendanceReportRow> source, bool absencesOnly)
    {
        var list = source.Where(x => !absencesOnly || !new[] { "HADIR", "LEWAT" }.Contains(x.Status.ToUpperInvariant())).ToList();
        r.Summary.Add(absencesOnly ? "Hanya rekod selain HADIR/LEWAT dipaparkan." : "Semua rekod kehadiran dalam tempoh pilihan.");
        r.Headers = new() { "Tarikh", "Nama Murid", "Kelas", "Status", "Kategori", "Sebab", "Catatan" };
        r.Rows = list.Select(x => new List<string> { D(x.Date), x.Nama, x.Kelas, x.Status, N(x.Kategori), N(x.Sebab), N(x.Catatan) }).ToList();
    }

    private static void BuildRisk(ReportDocument r, AnalyticsReportData x)
    {
        r.Summary.Add("Skor risiko = Ponteng×4 + Tidak Hadir×2 + Lewat.");
        r.Headers = new() { "Nama Murid", "Kelas", "TH", "Ponteng", "Lewat", "% Hadir", "Risiko", "Kes Aktif" };
        r.Rows = x.RiskStudents.Select(a => new List<string> { a.Nama, a.Kelas, a.TidakHadir.ToString(), a.Ponteng.ToString(), a.Lewat.ToString(), a.AttendanceRateText, a.RiskText, a.ActiveCaseCount.ToString() }).ToList();
    }

    private static void BuildCases(ReportDocument r, IEnumerable<CaseProfile> src)
    {
        r.Headers = new() { "Tarikh", "Nama Murid", "Kelas", "Kategori", "Status", "Guru", "Ringkasan" };
        r.Rows = src.OrderByDescending(x => x.OpenedAt).Select(x => new List<string> { D(x.OpenedAt), x.StudentName, x.Kelas, x.Category, x.Status, N(x.TeacherName), N(x.Summary) }).ToList();
    }
    private static void BuildRecovered(ReportDocument r, IEnumerable<CaseProfile> src)
    {
        r.Headers = new() { "Tarikh Pulih", "Nama Murid", "Kelas", "Kategori", "Guru", "Hasil Pemulihan", "Catatan" };
        r.Rows = src.OrderByDescending(x => x.RecoveredAt).Select(x => new List<string> { x.RecoveredAt.HasValue ? D(x.RecoveredAt.Value) : "—", x.StudentName, x.Kelas, x.Category, N(x.RecoveryTeacherName), N(x.RecoveryOutcome), N(x.RecoveryNotes) }).ToList();
    }
    private static void BuildGuardian(ReportDocument r, IEnumerable<GuardianContact> src)
    {
        r.Headers = new() { "Tarikh", "Nama Murid", "Kelas", "Kaedah", "Penjaga", "Telefon", "Hasil" };
        r.Rows = src.OrderByDescending(x => x.ContactDate).Select(x => new List<string> { D(x.ContactDate), x.StudentName, x.Kelas, x.Method, N(x.GuardianName), N(x.Phone), N(x.Outcome) }).ToList();
    }
    private static void BuildVisits(ReportDocument r, IEnumerable<HomeVisit> src)
    {
        r.Headers = new() { "Tarikh", "Nama Murid", "Kelas", "Guru", "Alamat", "Hasil", "Susulan" };
        r.Rows = src.OrderByDescending(x => x.VisitDate).Select(x => new List<string> { D(x.VisitDate), x.StudentName, x.Kelas, N(x.TeacherName), N(x.Address), N(x.Outcome), N(x.FollowUp) }).ToList();
    }
    private static void BuildCounselling(ReportDocument r, IEnumerable<CounsellingSession> src)
    {
        r.Headers = new() { "Tarikh", "Nama Murid", "Kelas", "Jenis", "Guru", "Ringkasan", "Tindakan Seterusnya" };
        r.Rows = src.OrderByDescending(x => x.SessionDate).Select(x => new List<string> { D(x.SessionDate), x.StudentName, x.Kelas, x.SessionType, N(x.TeacherName), N(x.Summary), N(x.NextAction) }).ToList();
    }
    private static void BuildInterventions(ReportDocument r, IEnumerable<InterventionRecord> src)
    {
        r.Headers = new() { "Tarikh", "Nama Murid", "Kelas", "Jenis", "Tindakan", "Guru", "Sasaran", "Status" };
        r.Rows = src.OrderByDescending(x => x.InterventionDate).Select(x => new List<string> { D(x.InterventionDate), x.StudentName, x.Kelas, x.InterventionType, N(x.Action), N(x.TeacherName), x.TargetDate.HasValue ? D(x.TargetDate.Value) : "—", x.Status }).ToList();
    }
    private static void BuildEvidence(ReportDocument r, IEnumerable<EvidenceFileRecord> src)
    {
        r.Headers = new() { "Tarikh", "Nama Murid", "Kelas", "Kategori", "Tajuk", "Fail", "Saiz", "Catatan" };
        r.Rows = src.OrderByDescending(x => x.EvidenceDate).Select(x => new List<string> { D(x.EvidenceDate), x.StudentName, x.Kelas, x.EvidenceType, N(x.DisplayTitle), N(x.FileName), x.FileSizeText, N(x.Notes) }).ToList();
    }
    private static void BuildStudents(ReportDocument r, IEnumerable<Student> src)
    {
        r.PeriodText = "Senarai semasa";
        r.Headers = new() { "No. Murid", "Nama Murid", "Kelas", "Tingkatan", "Penjaga", "Telefon", "Alamat" };
        r.Rows = src.OrderBy(x => x.Kelas).ThenBy(x => x.Nama).Select(x => new List<string> { N(x.NoMurid), x.Nama, x.Kelas, N(x.Tingkatan), N(x.Penjaga), N(x.Telefon), N(x.Alamat) }).ToList();
    }
    private static void BuildTeachers(ReportDocument r, IEnumerable<Teacher> src)
    {
        r.PeriodText = "Senarai semasa";
        r.ClassText = "SEMUA";
        r.Headers = new() { "Nama Guru", "Jawatan", "Telefon" };
        r.Rows = src.OrderBy(x => x.Nama).Select(x => new List<string> { x.Nama, N(x.Jawatan), N(x.Telefon) }).ToList();
    }
}
