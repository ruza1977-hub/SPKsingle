namespace SistemPengurusanKehadiran.Models;

public sealed class CaseProfile
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime OpenedAt { get; set; } = DateTime.Today;
    public string Status { get; set; } = "AKTIF";
    public string Category { get; set; } = "PONTENG";
    public string Summary { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime? RecoveredAt { get; set; }
    public string RecoveryOutcome { get; set; } = "";
    public string RecoveryTeacherId { get; set; } = "";
    public string RecoveryTeacherName { get; set; } = "";
    public DateTime? MonitoringUntil { get; set; }
    public string RecoveryNotes { get; set; } = "";
    public string SyncStatus { get; set; } = "pending";
    public string DateText => OpenedAt.ToString("dd/MM/yyyy");
    public string RecoveryDateText => RecoveredAt?.ToString("dd/MM/yyyy") ?? "—";
    public string MonitoringText => MonitoringUntil?.ToString("dd/MM/yyyy") ?? "—";
    public string RecoveryTeacherDisplay => string.IsNullOrWhiteSpace(RecoveryTeacherName) ? "Guru belum dipilih" : RecoveryTeacherName;
    public string StudentDisplay => string.IsNullOrWhiteSpace(Kelas) ? StudentName : $"{StudentName} • {Kelas}";
}

public sealed class RecoveryStudentOption
{
    public string StudentId { get; set; } = "";
    public string Display { get; set; } = "SEMUA MURID";
}

public sealed class GuardianContact
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime ContactDate { get; set; } = DateTime.Today;
    public string Method { get; set; } = "TELEFON";
    public string GuardianName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SyncStatus { get; set; } = "pending";
    public string DateText => ContactDate.ToString("dd/MM/yyyy");
}

public sealed class HomeVisit
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime VisitDate { get; set; } = DateTime.Today;
    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string Address { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string FollowUp { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SyncStatus { get; set; } = "pending";
    public string DateText => VisitDate.ToString("dd/MM/yyyy");
    public string TeacherDisplay => string.IsNullOrWhiteSpace(TeacherName) ? "Guru belum dipilih" : TeacherName;
}

public sealed class CounsellingSession
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime SessionDate { get; set; } = DateTime.Today;
    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string SessionType { get; set; } = "INDIVIDU";
    public string Summary { get; set; } = "";
    public string NextAction { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SyncStatus { get; set; } = "pending";
    public string DateText => SessionDate.ToString("dd/MM/yyyy");
    public string TeacherDisplay => string.IsNullOrWhiteSpace(TeacherName) ? "Guru belum dipilih" : TeacherName;
}

public sealed class InterventionRecord
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime InterventionDate { get; set; } = DateTime.Today;
    public string InterventionType { get; set; } = "KEHADIRAN";
    public string Action { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public DateTime? TargetDate { get; set; }
    public string Status { get; set; } = "DIRANCANG";
    public string Result { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SyncStatus { get; set; } = "pending";
    public string DateText => InterventionDate.ToString("dd/MM/yyyy");
    public string ActionPreview => Action.Length > 70 ? Action[..70] + "…" : Action;
}
