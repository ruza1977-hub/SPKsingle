using Microsoft.Data.Sqlite;
using SistemPengurusanKehadiran.Models;
using System.Globalization;

namespace SistemPengurusanKehadiran.Services;

public static class WarningLetterDatabase
{
    private const string Iso = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public static int AttendanceAbsenceCount(this DatabaseService db, string studentId, DateTime through)
    {
        using var c = db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM attendance
            WHERE school_id=$sid AND student_id=$student AND deleted_at IS NULL AND tarikh<=$date
              AND status IN ('TIDAK HADIR','PONTENG','MC','CUTI BERSEBAB')";
        cmd.Parameters.AddWithValue("$sid", db.SchoolId);
        cmd.Parameters.AddWithValue("$student", studentId);
        cmd.Parameters.AddWithValue("$date", through.ToString("yyyy-MM-dd"));
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public static List<WarningLetter> GetWarningLetters(this DatabaseService db, string search = "")
    {
        using var c = db.OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT w.id,w.student_id,s.nama,s.kelas,w.issue_date,w.warning_type,w.reference_no,
               w.guardian_name,w.guardian_address,w.total_absences,w.title,w.body,w.notes,w.pdf_data,w.sync_status
          FROM warning_letters w JOIN students s ON s.id=w.student_id
         WHERE w.school_id=$sid AND w.deleted_at IS NULL
           AND ($q='' OR lower(s.nama||' '||s.kelas||' '||w.warning_type||' '||w.reference_no||' '||w.title) LIKE '%'||lower($q)||'%')
         ORDER BY w.issue_date DESC,s.nama ASC";
        cmd.Parameters.AddWithValue("$sid", db.SchoolId);
        cmd.Parameters.AddWithValue("$q", search.Trim());
        using var r = cmd.ExecuteReader();
        var list = new List<WarningLetter>();
        while (r.Read())
        {
            list.Add(new WarningLetter
            {
                Id = r.GetString(0), StudentId = r.GetString(1), StudentName = r.GetString(2), Kelas = r.GetString(3),
                IssueDate = DateTime.TryParseExact(r.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Today,
                WarningType = r.GetString(5), ReferenceNo = r.GetString(6), GuardianName = r.GetString(7), GuardianAddress = r.GetString(8),
                TotalAbsences = r.GetInt32(9), Title = r.GetString(10), Body = r.GetString(11), Notes = r.GetString(12),
                PdfData = r.IsDBNull(13) ? null : (byte[])r[13], SyncStatus = r.GetString(14)
            });
        }
        return list;
    }

    public static void SaveWarningLetter(this DatabaseService db, WarningLetter x)
    {
        using var c = db.OpenConnection();
        var now = DateTime.UtcNow.ToString(Iso, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(x.Id)) x.Id = Guid.NewGuid().ToString();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"INSERT INTO warning_letters
            (id,school_id,student_id,issue_date,warning_type,reference_no,guardian_name,guardian_address,total_absences,title,body,notes,pdf_data,created_at,updated_at,version,sync_status)
            VALUES($id,$sid,$student,$date,$type,$ref,$guardian,$address,$total,$title,$body,$notes,$pdf,$now,$now,1,'pending')
            ON CONFLICT(id) DO UPDATE SET student_id=$student,issue_date=$date,warning_type=$type,reference_no=$ref,
              guardian_name=$guardian,guardian_address=$address,total_absences=$total,title=$title,body=$body,notes=$notes,
              pdf_data=$pdf,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";
        cmd.Parameters.AddWithValue("$id", x.Id); cmd.Parameters.AddWithValue("$sid", db.SchoolId); cmd.Parameters.AddWithValue("$student", x.StudentId);
        cmd.Parameters.AddWithValue("$date", x.IssueDate.ToString("yyyy-MM-dd")); cmd.Parameters.AddWithValue("$type", x.WarningType); cmd.Parameters.AddWithValue("$ref", x.ReferenceNo.Trim());
        cmd.Parameters.AddWithValue("$guardian", x.GuardianName.Trim()); cmd.Parameters.AddWithValue("$address", x.GuardianAddress.Trim()); cmd.Parameters.AddWithValue("$total", x.TotalAbsences);
        cmd.Parameters.AddWithValue("$title", x.Title.Trim()); cmd.Parameters.AddWithValue("$body", x.Body.Trim()); cmd.Parameters.AddWithValue("$notes", x.Notes.Trim());
        cmd.Parameters.Add("$pdf", SqliteType.Blob).Value = x.PdfData ?? Array.Empty<byte>(); cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
    }

    public static void DeleteWarningLetter(this DatabaseService db, string id)
    {
        using var c = db.OpenConnection(); using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE warning_letters SET deleted_at=$now,updated_at=$now,version=version+1,sync_status='pending' WHERE id=$id AND school_id=$sid";
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString(Iso, CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("$id", id); cmd.Parameters.AddWithValue("$sid", db.SchoolId); cmd.ExecuteNonQuery();
    }
}
