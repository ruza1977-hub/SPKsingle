using Microsoft.Data.Sqlite;
using SistemPengurusanKehadiran.Helpers;
using SistemPengurusanKehadiran.Models;
using System.Globalization;

namespace SistemPengurusanKehadiran.Services;

public sealed class DatabaseService
{
    public static DatabaseService Instance { get; } = new();
    public string SchoolId { get; private set; } = "";
    public string DatabasePath => AppPaths.Database;
    private const string Iso = "yyyy-MM-ddTHH:mm:ss.fffZ";
    private DatabaseService() { }

    public SqliteConnection OpenConnection()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var c = new SqliteConnection($"Data Source={DatabasePath};Cache=Shared;Pooling=False");
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return c;
    }

    public void Initialize()
    {
        MigrateLegacyWindowsDatabaseIfNeeded();
        using var c = OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = MigrationSql;
        cmd.ExecuteNonQuery();
        EnsureRecoveryColumns(c);
        SchoolId = EnsureSchool(c);
        EnsureSchoolBindingState(c);
        EnsureDeviceId(c);
    }

    private static void MigrateLegacyWindowsDatabaseIfNeeded()
    {
        var canonical = AppPaths.Database;
        var legacy = AppPaths.LegacyWindowsDatabase;
        if (File.Exists(canonical) || !File.Exists(legacy)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(canonical)!);
        File.Move(legacy, canonical);

        // Jika versi lama ditutup ketika WAL masih ada, pindahkan sidecar bersama DB.
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var oldSidecar = legacy + suffix;
            var newSidecar = canonical + suffix;
            if (File.Exists(oldSidecar)) File.Move(oldSidecar, newSidecar);
        }
    }

    private static void EnsureRecoveryColumns(SqliteConnection c)
    {
        var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var info=c.CreateCommand())
        {
            info.CommandText="PRAGMA table_info(case_profiles)";
            using var r=info.ExecuteReader();
            while(r.Read()) existing.Add(r.GetString(1));
        }
        var columns=new (string Name,string Sql)[]
        {
            ("recovered_at","ALTER TABLE case_profiles ADD COLUMN recovered_at TEXT"),
            ("recovery_outcome","ALTER TABLE case_profiles ADD COLUMN recovery_outcome TEXT NOT NULL DEFAULT ''"),
            ("recovery_teacher_id","ALTER TABLE case_profiles ADD COLUMN recovery_teacher_id TEXT NOT NULL DEFAULT ''"),
            ("monitoring_until","ALTER TABLE case_profiles ADD COLUMN monitoring_until TEXT"),
            ("recovery_notes","ALTER TABLE case_profiles ADD COLUMN recovery_notes TEXT NOT NULL DEFAULT ''")
        };
        foreach(var column in columns)
        {
            if(existing.Contains(column.Name)) continue;
            using var add=c.CreateCommand();add.CommandText=column.Sql;add.ExecuteNonQuery();
        }
    }


    private void EnsureSchoolBindingState(SqliteConnection c)
    {
        using var q = c.CreateCommand();
        q.CommandText = "SELECT value FROM settings WHERE key='school_binding_state' LIMIT 1";
        var state = q.ExecuteScalar()?.ToString();
        if (!string.IsNullOrWhiteSpace(state)) return;

        // Versi lama belum mempunyai penanda provisioning. Hanya DB benar-benar kosong
        // dengan profil sekolah lalai dianggap belum terikat kepada tenant.
        using var profile = c.CreateCommand();
        profile.CommandText = "SELECT name,code FROM schools WHERE school_id=$s LIMIT 1";
        profile.Parameters.AddWithValue("$s", SchoolId);
        using var r = profile.ExecuteReader();
        var defaultProfile = r.Read() &&
            string.Equals(r.GetString(0), "SEKOLAH", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(r.GetString(1));
        r.Close();

        var pristine = defaultProfile && IsTenantDataEmpty(c);
        using var set = c.CreateCommand();
        set.CommandText = "INSERT INTO settings(key,value) VALUES('school_binding_state',$v) ON CONFLICT(key) DO UPDATE SET value=$v";
        set.Parameters.AddWithValue("$v", pristine ? "unbound" : "bound");
        set.ExecuteNonQuery();
    }

    private static bool IsTenantDataEmpty(SqliteConnection c)
    {
        var tables = new[]
        {
            "students","teachers","calendar_days","attendance","case_profiles",
            "guardian_contacts","home_visits","counselling_sessions","interventions",
            "warning_letters","evidence_files","video_evidence","sync_conflicts"
        };
        foreach (var table in tables)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT EXISTS(SELECT 1 FROM {table} LIMIT 1)";
            if (Convert.ToInt32(cmd.ExecuteScalar() ?? 0) != 0) return false;
        }
        return true;
    }

    private static void EnsureDeviceId(SqliteConnection c)
    {
        using var q = c.CreateCommand();
        q.CommandText = "SELECT value FROM sync_state WHERE key='device_id' LIMIT 1";
        if (!string.IsNullOrWhiteSpace(q.ExecuteScalar()?.ToString())) return;
        using var set = c.CreateCommand();
        set.CommandText = "INSERT INTO sync_state(key,value) VALUES('device_id',$v) ON CONFLICT(key) DO UPDATE SET value=$v";
        set.Parameters.AddWithValue("$v", Guid.NewGuid().ToString());
        set.ExecuteNonQuery();
    }

    public bool TryBindToServerSchool(string serverSchoolId, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(serverSchoolId))
        {
            reason = "school_id Server Config tiada";
            return false;
        }

        using var c = OpenConnection();
        using var stateCmd = c.CreateCommand();
        stateCmd.CommandText = "SELECT value FROM settings WHERE key='school_binding_state' LIMIT 1";
        var state = stateCmd.ExecuteScalar()?.ToString() ?? "bound";

        if (string.Equals(SchoolId, serverSchoolId, StringComparison.OrdinalIgnoreCase))
        {
            using var mark = c.CreateCommand();
            mark.CommandText = "INSERT INTO settings(key,value) VALUES('school_binding_state','bound') ON CONFLICT(key) DO UPDATE SET value='bound'";
            mark.ExecuteNonQuery();
            reason = "School ID sudah sepadan";
            return true;
        }

        if (!string.Equals(state, "unbound", StringComparison.OrdinalIgnoreCase))
        {
            reason = "SQLite ini sudah terikat kepada sekolah lain. Restore backup sekolah yang betul atau gunakan Server Config sekolah yang sepadan.";
            return false;
        }

        var oldSchoolId = SchoolId;
        using var tx = c.BeginTransaction();
        try
        {
            // Tukar semua rekod tenant yang dicipta sebelum provisioning kepada School ID server.
            foreach (var table in SyncTables)
            {
                using var update = c.CreateCommand();
                update.Transaction = tx;
                update.CommandText = $"UPDATE {table} SET school_id=$new WHERE school_id=$old";
                update.Parameters.AddWithValue("$new", serverSchoolId);
                update.Parameters.AddWithValue("$old", oldSchoolId);
                update.ExecuteNonQuery();
            }

            // Rekod sekolah placeholder menggunakan id yang sama dengan school_id.
            using (var school = c.CreateCommand())
            {
                school.Transaction = tx;
                school.CommandText = "UPDATE schools SET id=$new,school_id=$new,sync_status='synced' WHERE id=$old OR school_id=$old";
                school.Parameters.AddWithValue("$new", serverSchoolId);
                school.Parameters.AddWithValue("$old", oldSchoolId);
                school.ExecuteNonQuery();
            }

            using (var settings = c.CreateCommand())
            {
                settings.Transaction = tx;
                settings.CommandText = @"
INSERT INTO settings(key,value) VALUES('school_id',$new)
ON CONFLICT(key) DO UPDATE SET value=$new;
INSERT INTO settings(key,value) VALUES('school_binding_state','bound')
ON CONFLICT(key) DO UPDATE SET value='bound';";
                settings.Parameters.AddWithValue("$new", serverSchoolId);
                settings.ExecuteNonQuery();
            }

            using (var reset = c.CreateCommand())
            {
                reset.Transaction = tx;
                reset.CommandText = "DELETE FROM sync_state WHERE key IN ('last_pull_revision','last_sync_at','last_auto_sync_at','last_auto_sync_error')";
                reset.ExecuteNonQuery();
            }

            tx.Commit();
            SchoolId = serverSchoolId;
            reason = "Komputer ini berjaya dipautkan kepada School ID Server Config";
            return true;
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { }
            reason = "Gagal memautkan School ID: " + ex.Message;
            return false;
        }
    }

    private string EnsureSchool(SqliteConnection c)
    {
        using var q = c.CreateCommand();
        q.CommandText = "SELECT value FROM settings WHERE key='school_id' LIMIT 1";
        var existing = q.ExecuteScalar()?.ToString();
        if (!string.IsNullOrWhiteSpace(existing)) return existing!;
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString(Iso, CultureInfo.InvariantCulture);
        using var tx = c.BeginTransaction();
        using var ins = c.CreateCommand(); ins.Transaction = tx;
        ins.CommandText = "INSERT INTO schools(id,school_id,name,code,active,created_at,updated_at,version,sync_status) VALUES($id,$id,'SEKOLAH','',1,$now,$now,1,'pending'); INSERT INTO settings(key,value) VALUES('school_id',$id);";
        ins.Parameters.AddWithValue("$id", id); ins.Parameters.AddWithValue("$now", now); ins.ExecuteNonQuery();
        tx.Commit(); return id;
    }

    public DashboardStats GetDashboardStats()
    {
        using var c = OpenConnection();
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var year = DateTime.Today.Year.ToString();
        return new DashboardStats {
            ActiveStudents = ScalarInt(c, "SELECT COUNT(*) FROM students WHERE school_id=$s AND deleted_at IS NULL AND aktif=1"),
            ActiveTeachers = ScalarInt(c, "SELECT COUNT(*) FROM teachers WHERE school_id=$s AND deleted_at IS NULL AND aktif=1"),
            SchoolDaysThisYear = ScalarInt(c, "SELECT COUNT(*) FROM calendar_days WHERE school_id=$s AND deleted_at IS NULL AND hari_persekolahan=1 AND substr(tarikh,1,4)=$x", year),
            HadirToday = ScalarInt(c, "SELECT COUNT(*) FROM attendance WHERE school_id=$s AND deleted_at IS NULL AND tarikh=$x AND status IN ('HADIR','LEWAT')", today),
            TidakHadirToday = ScalarInt(c, "SELECT COUNT(*) FROM attendance WHERE school_id=$s AND deleted_at IS NULL AND tarikh=$x AND status IN ('TIDAK HADIR','PONTENG','MC','CUTI BERSEBAB')", today),
            ActiveCases = ScalarInt(c, "SELECT COUNT(*) FROM case_profiles WHERE school_id=$s AND deleted_at IS NULL AND status='AKTIF'"),
            RecoveredCases = ScalarInt(c, "SELECT COUNT(*) FROM case_profiles WHERE school_id=$s AND deleted_at IS NULL AND status='SELESAI'"),
            PendingSync = PendingSyncCount(c)
        };
    }

    private int ScalarInt(SqliteConnection c, string sql, string? x=null)
    {
        using var cmd=c.CreateCommand(); cmd.CommandText=sql; cmd.Parameters.AddWithValue("$s",SchoolId); if(x!=null) cmd.Parameters.AddWithValue("$x",x); return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public List<Student> GetStudents(string search="")
    {
        using var c=OpenConnection(); using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT id,school_id,no_murid,nama,kelas,tingkatan,penjaga,telefon,alamat,aktif FROM students WHERE school_id=$s AND deleted_at IS NULL AND ($q='' OR lower(nama||' '||kelas||' '||no_murid) LIKE '%'||lower($q)||'%') ORDER BY kelas,nama";
        cmd.Parameters.AddWithValue("$s",SchoolId);cmd.Parameters.AddWithValue("$q",search.Trim()); using var r=cmd.ExecuteReader(); var list=new List<Student>(); while(r.Read()) list.Add(new Student{Id=r.GetString(0),SchoolId=r.GetString(1),NoMurid=r.GetString(2),Nama=r.GetString(3),Kelas=r.GetString(4),Tingkatan=r.GetString(5),Penjaga=r.GetString(6),Telefon=r.GetString(7),Alamat=r.GetString(8),Aktif=r.GetInt32(9)==1}); return list;
    }

    public void SaveStudent(Student s)
    {
        using var c=OpenConnection(); var now=DateTime.UtcNow.ToString(Iso,CultureInfo.InvariantCulture); if(string.IsNullOrWhiteSpace(s.Id)) s.Id=Guid.NewGuid().ToString();
        using var cmd=c.CreateCommand(); cmd.CommandText=@"INSERT INTO students(id,school_id,no_murid,nama,kelas,tingkatan,penjaga,telefon,alamat,aktif,created_at,updated_at,version,sync_status) VALUES($id,$sid,$no,$nama,$kelas,$ting,$pen,$tel,$alamat,$aktif,$now,$now,1,'pending') ON CONFLICT(id) DO UPDATE SET no_murid=$no,nama=$nama,kelas=$kelas,tingkatan=$ting,penjaga=$pen,telefon=$tel,alamat=$alamat,aktif=$aktif,updated_at=$now,version=version+1,sync_status='pending';";
        cmd.Parameters.AddWithValue("$id",s.Id);cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$no",s.NoMurid.Trim());cmd.Parameters.AddWithValue("$nama",s.Nama.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("$kelas",s.Kelas.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("$ting",s.Tingkatan.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("$pen",s.Penjaga.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("$tel",s.Telefon.Trim());cmd.Parameters.AddWithValue("$alamat",s.Alamat.Trim());cmd.Parameters.AddWithValue("$aktif",s.Aktif?1:0);cmd.Parameters.AddWithValue("$now",now);cmd.ExecuteNonQuery();
    }
    public void DeleteStudent(string id)=>SoftDelete("students",id);

    public List<Teacher> GetTeachers()
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT id,nama,jawatan,telefon,aktif FROM teachers WHERE school_id=$s AND deleted_at IS NULL ORDER BY nama";cmd.Parameters.AddWithValue("$s",SchoolId);using var r=cmd.ExecuteReader();var list=new List<Teacher>();while(r.Read())list.Add(new Teacher{Id=r.GetString(0),Nama=r.GetString(1),Jawatan=r.GetString(2),Telefon=r.GetString(3),Aktif=r.GetInt32(4)==1});return list;
    }
    public void SaveTeacher(Teacher t)
    {
        using var c=OpenConnection();var now=DateTime.UtcNow.ToString(Iso,CultureInfo.InvariantCulture);if(string.IsNullOrWhiteSpace(t.Id))t.Id=Guid.NewGuid().ToString();using var cmd=c.CreateCommand();cmd.CommandText=@"INSERT INTO teachers(id,school_id,nama,jawatan,telefon,aktif,created_at,updated_at,version,sync_status) VALUES($id,$sid,$nama,$jaw,$tel,$aktif,$now,$now,1,'pending') ON CONFLICT(id) DO UPDATE SET nama=$nama,jawatan=$jaw,telefon=$tel,aktif=$aktif,updated_at=$now,version=version+1,sync_status='pending'";cmd.Parameters.AddWithValue("$id",t.Id);cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$nama",t.Nama.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("$jaw",t.Jawatan.Trim());cmd.Parameters.AddWithValue("$tel",t.Telefon.Trim());cmd.Parameters.AddWithValue("$aktif",t.Aktif?1:0);cmd.Parameters.AddWithValue("$now",now);cmd.ExecuteNonQuery();
    }
    public void DeleteTeacher(string id)=>SoftDelete("teachers",id);

    public List<CalendarDay> GetCalendarDays(int year)
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT id,tarikh,jenis,tajuk,hari_persekolahan,minggu_akademik,catatan FROM calendar_days WHERE school_id=$s AND deleted_at IS NULL AND substr(tarikh,1,4)=$y ORDER BY tarikh";cmd.Parameters.AddWithValue("$s",SchoolId);cmd.Parameters.AddWithValue("$y",year.ToString());using var r=cmd.ExecuteReader();var list=new List<CalendarDay>();while(r.Read())list.Add(new CalendarDay{Id=r.GetString(0),Tarikh=DateTime.ParseExact(r.GetString(1),"yyyy-MM-dd",CultureInfo.InvariantCulture),Jenis=r.GetString(2),Tajuk=r.GetString(3),HariPersekolahan=r.GetInt32(4)==1,MingguAkademik=r.IsDBNull(5)?null:r.GetInt32(5),Catatan=r.GetString(6)});return list;
    }
    public bool? IsSchoolDay(DateTime date)
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT hari_persekolahan FROM calendar_days WHERE school_id=$s AND tarikh=$d AND deleted_at IS NULL LIMIT 1";cmd.Parameters.AddWithValue("$s",SchoolId);cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));var v=cmd.ExecuteScalar();return v is null?null:Convert.ToInt32(v)==1;
    }
    public void SaveCalendarDay(CalendarDay d)
    {
        using var c=OpenConnection();var now=DateTime.UtcNow.ToString(Iso,CultureInfo.InvariantCulture);if(string.IsNullOrWhiteSpace(d.Id))d.Id=Guid.NewGuid().ToString();using var cmd=c.CreateCommand();cmd.CommandText=@"INSERT INTO calendar_days(id,school_id,tarikh,jenis,tajuk,hari_persekolahan,minggu_akademik,catatan,sumber,created_at,updated_at,version,sync_status) VALUES($id,$sid,$d,$j,$t,$h,$m,$c,'MANUAL',$now,$now,1,'pending') ON CONFLICT(school_id,tarikh) DO UPDATE SET jenis=$j,tajuk=$t,hari_persekolahan=$h,minggu_akademik=$m,catatan=$c,updated_at=$now,version=version+1,sync_status='pending'";cmd.Parameters.AddWithValue("$id",d.Id);cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$d",d.Tarikh.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$j",d.Jenis);cmd.Parameters.AddWithValue("$t",d.Tajuk);cmd.Parameters.AddWithValue("$h",d.HariPersekolahan?1:0);cmd.Parameters.AddWithValue("$m",(object?)d.MingguAkademik??DBNull.Value);cmd.Parameters.AddWithValue("$c",d.Catatan);cmd.Parameters.AddWithValue("$now",now);cmd.ExecuteNonQuery();
    }

    public List<string> GetClasses()=>GetStudents().Select(x=>x.Kelas).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x=>x).ToList();
    public List<AttendanceRow> LoadAttendance(DateTime date,string kelas)
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT s.id,s.no_murid,s.nama,s.kelas,COALESCE(a.status,''),COALESCE(a.kategori,''),COALESCE(a.sebab,''),COALESCE(a.catatan,'') FROM students s LEFT JOIN attendance a ON a.school_id=s.school_id AND a.student_id=s.id AND a.tarikh=$d AND a.deleted_at IS NULL WHERE s.school_id=$s AND s.deleted_at IS NULL AND s.aktif=1 AND ($k='SEMUA KELAS' OR s.kelas=$k) ORDER BY s.nama";cmd.Parameters.AddWithValue("$s",SchoolId);cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$k",kelas);using var r=cmd.ExecuteReader();var list=new List<AttendanceRow>();while(r.Read())list.Add(new AttendanceRow{StudentId=r.GetString(0),NoMurid=r.GetString(1),Nama=r.GetString(2),Kelas=r.GetString(3),Status=string.IsNullOrWhiteSpace(r.GetString(4))?"HADIR":r.GetString(4),Kategori=r.GetString(5),Sebab=r.GetString(6),Catatan=r.GetString(7)});return list;
    }
    public void SaveAttendance(DateTime date,IEnumerable<AttendanceRow> rows)
    {
        using var c=OpenConnection();using var tx=c.BeginTransaction();var now=DateTime.UtcNow.ToString(Iso,CultureInfo.InvariantCulture);foreach(var row in rows){using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=@"INSERT INTO attendance(id,school_id,student_id,tarikh,status,kategori,sebab,catatan,created_at,updated_at,version,sync_status) VALUES($id,$sid,$st,$d,$status,$kat,$sebab,$cat,$now,$now,1,'pending') ON CONFLICT(school_id,student_id,tarikh) DO UPDATE SET status=$status,kategori=$kat,sebab=$sebab,catatan=$cat,updated_at=$now,version=version+1,sync_status='pending'";cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$st",row.StudentId);cmd.Parameters.AddWithValue("$d",date.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$status",row.Status);cmd.Parameters.AddWithValue("$kat",row.Kategori);cmd.Parameters.AddWithValue("$sebab",row.Sebab);cmd.Parameters.AddWithValue("$cat",row.Catatan);cmd.Parameters.AddWithValue("$now",now);cmd.ExecuteNonQuery();}tx.Commit();
    }

    public List<AttendanceReportRow> GetAttendanceReportRows(DateTime startDate, DateTime endDate, string kelas = "SEMUA KELAS")
    {
        using var c = OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT a.tarikh,s.id,s.no_murid,s.nama,s.kelas,a.status,COALESCE(a.kategori,''),COALESCE(a.sebab,''),COALESCE(a.catatan,'')
FROM attendance a
JOIN students s ON s.id=a.student_id AND s.school_id=a.school_id
WHERE a.school_id=$sid AND a.deleted_at IS NULL AND s.deleted_at IS NULL
AND a.tarikh BETWEEN $start AND $end
AND ($kelas='SEMUA KELAS' OR s.kelas=$kelas)
ORDER BY a.tarikh,s.kelas,s.nama";
        cmd.Parameters.AddWithValue("$sid", SchoolId);
        cmd.Parameters.AddWithValue("$start", startDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$end", endDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$kelas", kelas);
        using var r = cmd.ExecuteReader();
        var list = new List<AttendanceReportRow>();
        while (r.Read())
        {
            list.Add(new AttendanceReportRow
            {
                Date = ParseDay(r.GetString(0)),
                StudentId = r.GetString(1),
                NoMurid = r.GetString(2),
                Nama = r.GetString(3),
                Kelas = r.GetString(4),
                Status = r.GetString(5),
                Kategori = r.GetString(6),
                Sebab = r.GetString(7),
                Catatan = r.GetString(8)
            });
        }
        return list;
    }

    public AnalyticsReportData GetAnalyticsReport(DateTime startDate, DateTime endDate, string kelas = "SEMUA KELAS")
    {
        var result = new AnalyticsReportData
        {
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            ClassFilter = string.IsNullOrWhiteSpace(kelas) ? "SEMUA KELAS" : kelas.Trim()
        };

        var start = result.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = result.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var classFilter = result.ClassFilter;
        using var c = OpenConnection();

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT COUNT(a.id),
COALESCE(SUM(CASE WHEN a.status IN ('HADIR','LEWAT') THEN 1 ELSE 0 END),0),
COALESCE(SUM(CASE WHEN a.status IN ('TIDAK HADIR','PONTENG','MC','CUTI BERSEBAB') THEN 1 ELSE 0 END),0),
COALESCE(SUM(CASE WHEN a.status='PONTENG' THEN 1 ELSE 0 END),0)
FROM attendance a JOIN students s ON s.id=a.student_id AND s.school_id=a.school_id
WHERE a.school_id=$sid AND a.deleted_at IS NULL AND s.deleted_at IS NULL AND s.aktif=1
AND a.tarikh BETWEEN $start AND $end
AND ($kelas='SEMUA KELAS' OR s.kelas=$kelas)";
            cmd.Parameters.AddWithValue("$sid", SchoolId);
            cmd.Parameters.AddWithValue("$start", start);
            cmd.Parameters.AddWithValue("$end", end);
            cmd.Parameters.AddWithValue("$kelas", classFilter);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                result.TotalRecords = Convert.ToInt32(r.GetInt64(0));
                result.PresentRecords = Convert.ToInt32(r.GetInt64(1));
                result.NotPresentRecords = Convert.ToInt32(r.GetInt64(2));
                result.PontengRecords = Convert.ToInt32(r.GetInt64(3));
            }
        }

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT COUNT(*) FROM case_profiles p
JOIN students s ON s.id=p.student_id AND s.school_id=p.school_id
WHERE p.school_id=$sid AND p.deleted_at IS NULL AND s.deleted_at IS NULL
AND p.status IN ('AKTIF','PEMANTAUAN') AND p.opened_at <= $end
AND ($kelas='SEMUA KELAS' OR s.kelas=$kelas)";
            cmd.Parameters.AddWithValue("$sid", SchoolId);
            cmd.Parameters.AddWithValue("$end", end);
            cmd.Parameters.AddWithValue("$kelas", classFilter);
            result.ActiveCases = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT COUNT(*) FROM case_profiles p
JOIN students s ON s.id=p.student_id AND s.school_id=p.school_id
WHERE p.school_id=$sid AND p.deleted_at IS NULL AND s.deleted_at IS NULL
AND p.status='SELESAI'
AND COALESCE(NULLIF(p.recovered_at,''),p.opened_at) BETWEEN $start AND $end
AND ($kelas='SEMUA KELAS' OR s.kelas=$kelas)";
            cmd.Parameters.AddWithValue("$sid", SchoolId);
            cmd.Parameters.AddWithValue("$start", start);
            cmd.Parameters.AddWithValue("$end", end);
            cmd.Parameters.AddWithValue("$kelas", classFilter);
            result.RecoveredCases = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }

        var monthNames = new[] { "", "JAN", "FEB", "MAC", "APR", "MEI", "JUN", "JUL", "OGO", "SEP", "OKT", "NOV", "DIS" };
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT CAST(substr(a.tarikh,6,2) AS INTEGER) AS bulan,
COUNT(a.id),
COALESCE(SUM(CASE WHEN a.status IN ('HADIR','LEWAT') THEN 1 ELSE 0 END),0)
FROM attendance a JOIN students s ON s.id=a.student_id AND s.school_id=a.school_id
WHERE a.school_id=$sid AND a.deleted_at IS NULL AND s.deleted_at IS NULL AND s.aktif=1
AND a.tarikh BETWEEN $start AND $end
AND ($kelas='SEMUA KELAS' OR s.kelas=$kelas)
GROUP BY bulan ORDER BY bulan";
            cmd.Parameters.AddWithValue("$sid", SchoolId);
            cmd.Parameters.AddWithValue("$start", start);
            cmd.Parameters.AddWithValue("$end", end);
            cmd.Parameters.AddWithValue("$kelas", classFilter);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var month = Convert.ToInt32(r.GetInt64(0));
                result.Trend.Add(new AnalyticsTrendRow
                {
                    Month = month,
                    MonthLabel = month >= 1 && month <= 12 ? monthNames[month] : month.ToString(CultureInfo.InvariantCulture),
                    Total = Convert.ToInt32(r.GetInt64(1)),
                    Present = Convert.ToInt32(r.GetInt64(2))
                });
            }
        }

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT s.kelas,
COALESCE(SUM(CASE WHEN a.status IN ('TIDAK HADIR','MC','CUTI BERSEBAB') THEN 1 ELSE 0 END),0),
COALESCE(SUM(CASE WHEN a.status='PONTENG' THEN 1 ELSE 0 END),0),
COUNT(a.id),
COALESCE(SUM(CASE WHEN a.status IN ('HADIR','LEWAT') THEN 1 ELSE 0 END),0)
FROM students s
LEFT JOIN attendance a ON a.student_id=s.id AND a.school_id=s.school_id AND a.deleted_at IS NULL AND a.tarikh BETWEEN $start AND $end
WHERE s.school_id=$sid AND s.deleted_at IS NULL AND s.aktif=1
AND ($kelas='SEMUA KELAS' OR s.kelas=$kelas)
GROUP BY s.kelas ORDER BY s.kelas";
            cmd.Parameters.AddWithValue("$sid", SchoolId);
            cmd.Parameters.AddWithValue("$start", start);
            cmd.Parameters.AddWithValue("$end", end);
            cmd.Parameters.AddWithValue("$kelas", classFilter);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                result.Classes.Add(new AnalyticsClassRow
                {
                    Kelas = r.IsDBNull(0) ? "" : r.GetString(0),
                    TidakHadir = Convert.ToInt32(r.GetInt64(1)),
                    Ponteng = Convert.ToInt32(r.GetInt64(2)),
                    Total = Convert.ToInt32(r.GetInt64(3)),
                    Present = Convert.ToInt32(r.GetInt64(4))
                });
            }
        }

        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT s.id,s.nama,s.kelas,
COALESCE(SUM(CASE WHEN a.status IN ('TIDAK HADIR','MC','CUTI BERSEBAB') THEN 1 ELSE 0 END),0) AS th,
COALESCE(SUM(CASE WHEN a.status='PONTENG' THEN 1 ELSE 0 END),0) AS ponteng,
COALESCE(SUM(CASE WHEN a.status='LEWAT' THEN 1 ELSE 0 END),0) AS lewat,
COUNT(a.id) AS total,
COALESCE(SUM(CASE WHEN a.status IN ('HADIR','LEWAT') THEN 1 ELSE 0 END),0) AS hadir,
(SELECT COUNT(*) FROM case_profiles cp WHERE cp.school_id=s.school_id AND cp.student_id=s.id AND cp.deleted_at IS NULL AND cp.status IN ('AKTIF','PEMANTAUAN')) AS kes
FROM students s
LEFT JOIN attendance a ON a.student_id=s.id AND a.school_id=s.school_id AND a.deleted_at IS NULL AND a.tarikh BETWEEN $start AND $end
WHERE s.school_id=$sid AND s.deleted_at IS NULL AND s.aktif=1
AND ($kelas='SEMUA KELAS' OR s.kelas=$kelas)
GROUP BY s.id,s.nama,s.kelas
HAVING (
COALESCE(SUM(CASE WHEN a.status IN ('TIDAK HADIR','MC','CUTI BERSEBAB') THEN 1 ELSE 0 END),0) +
COALESCE(SUM(CASE WHEN a.status='PONTENG' THEN 1 ELSE 0 END),0) +
COALESCE(SUM(CASE WHEN a.status='LEWAT' THEN 1 ELSE 0 END),0)) > 0
ORDER BY (
COALESCE(SUM(CASE WHEN a.status='PONTENG' THEN 1 ELSE 0 END),0)*4 +
COALESCE(SUM(CASE WHEN a.status IN ('TIDAK HADIR','MC','CUTI BERSEBAB') THEN 1 ELSE 0 END),0)*2 +
COALESCE(SUM(CASE WHEN a.status='LEWAT' THEN 1 ELSE 0 END),0)) DESC, s.nama
LIMIT 100";
            cmd.Parameters.AddWithValue("$sid", SchoolId);
            cmd.Parameters.AddWithValue("$start", start);
            cmd.Parameters.AddWithValue("$end", end);
            cmd.Parameters.AddWithValue("$kelas", classFilter);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                result.RiskStudents.Add(new AnalyticsRiskRow
                {
                    StudentId = r.GetString(0),
                    Nama = r.GetString(1),
                    Kelas = r.GetString(2),
                    TidakHadir = Convert.ToInt32(r.GetInt64(3)),
                    Ponteng = Convert.ToInt32(r.GetInt64(4)),
                    Lewat = Convert.ToInt32(r.GetInt64(5)),
                    Total = Convert.ToInt32(r.GetInt64(6)),
                    Present = Convert.ToInt32(r.GetInt64(7)),
                    ActiveCaseCount = Convert.ToInt32(r.GetInt64(8))
                });
            }
        }

        return result;
    }

    public List<(string Nama,string Kelas,int TidakHadir,int Ponteng,int Lewat)> GetRiskStudents(int year)
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT s.nama,s.kelas,SUM(CASE WHEN a.status IN ('TIDAK HADIR','MC','CUTI BERSEBAB') THEN 1 ELSE 0 END),SUM(CASE WHEN a.status='PONTENG' THEN 1 ELSE 0 END),SUM(CASE WHEN a.status='LEWAT' THEN 1 ELSE 0 END) FROM students s LEFT JOIN attendance a ON a.student_id=s.id AND a.school_id=s.school_id AND a.deleted_at IS NULL AND substr(a.tarikh,1,4)=$y WHERE s.school_id=$s AND s.deleted_at IS NULL GROUP BY s.id HAVING COUNT(a.id)>0 ORDER BY 4 DESC,3 DESC,5 DESC LIMIT 50";cmd.Parameters.AddWithValue("$s",SchoolId);cmd.Parameters.AddWithValue("$y",year.ToString());using var r=cmd.ExecuteReader();var list=new List<(string,string,int,int,int)>();while(r.Read())list.Add((r.GetString(0),r.GetString(1),r.GetInt32(2),r.GetInt32(3),r.GetInt32(4)));return list;
    }

    public (string Name,string Code) GetSchoolProfile(){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT name,code FROM schools WHERE school_id=$s AND deleted_at IS NULL LIMIT 1";cmd.Parameters.AddWithValue("$s",SchoolId);using var r=cmd.ExecuteReader();return r.Read()?(r.GetString(0),r.GetString(1)):("SEKOLAH","");}
    public void SaveSchoolProfile(string name,string code){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="UPDATE schools SET name=$n,code=$c,updated_at=$u,version=version+1,sync_status='pending' WHERE school_id=$s";cmd.Parameters.AddWithValue("$n",name.Trim());cmd.Parameters.AddWithValue("$c",code.Trim());cmd.Parameters.AddWithValue("$u",DateTime.UtcNow.ToString(Iso,CultureInfo.InvariantCulture));cmd.Parameters.AddWithValue("$s",SchoolId);cmd.ExecuteNonQuery();}

    public int PendingSyncCount(){using var c=OpenConnection();return PendingSyncCount(c);}    
    private int PendingSyncCount(SqliteConnection c){var total=0;foreach(var t in SyncTables){using var cmd=c.CreateCommand();cmd.CommandText=$"SELECT COUNT(*) FROM {t} WHERE school_id=$s AND sync_status='pending'";cmd.Parameters.AddWithValue("$s",SchoolId);total+=Convert.ToInt32(cmd.ExecuteScalar()??0);}return total;}
    public int ConflictCount(){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM sync_conflicts WHERE school_id=$s AND resolved=0";cmd.Parameters.AddWithValue("$s",SchoolId);return Convert.ToInt32(cmd.ExecuteScalar()??0);}
    public string GetSyncState(string key){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="SELECT value FROM sync_state WHERE key=$k";cmd.Parameters.AddWithValue("$k",key);return cmd.ExecuteScalar()?.ToString()??"";}
    public void SetSyncState(string key,string value){using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO sync_state(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=$v";cmd.Parameters.AddWithValue("$k",key);cmd.Parameters.AddWithValue("$v",value);cmd.ExecuteNonQuery();}

    public void SoftDelete(string table,string id){if(!SyncTables.Contains(table))throw new InvalidOperationException("Table tidak dibenarkan");using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=$"UPDATE {table} SET deleted_at=$now,updated_at=$now,version=version+1,sync_status='pending' WHERE id=$id AND school_id=$s";cmd.Parameters.AddWithValue("$now",DateTime.UtcNow.ToString(Iso,CultureInfo.InvariantCulture));cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$s",SchoolId);cmd.ExecuteNonQuery();}


    // MARK: Modul Kes v0.3
    public List<CaseProfile> GetCaseProfiles(string search="")
    {
        using var c=OpenConnection(); using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT p.id,p.student_id,s.nama,s.kelas,p.opened_at,p.status,p.category,p.summary,p.teacher_id,COALESCE(t.nama,''),p.notes,p.sync_status,
COALESCE(p.recovered_at,''),COALESCE(p.recovery_outcome,''),COALESCE(p.recovery_teacher_id,''),COALESCE(rt.nama,''),COALESCE(p.monitoring_until,''),COALESCE(p.recovery_notes,'')
FROM case_profiles p JOIN students s ON s.id=p.student_id
LEFT JOIN teachers t ON t.id=p.teacher_id AND t.deleted_at IS NULL
LEFT JOIN teachers rt ON rt.id=p.recovery_teacher_id AND rt.deleted_at IS NULL
WHERE p.school_id=$sid AND p.deleted_at IS NULL
AND ($q='' OR lower(s.nama||' '||s.kelas||' '||p.category||' '||p.status||' '||p.summary||' '||COALESCE(p.recovery_outcome,'')) LIKE '%'||lower($q)||'%')
ORDER BY CASE WHEN p.status='SELESAI' THEN 1 ELSE 0 END,p.opened_at DESC,s.nama";
        cmd.Parameters.AddWithValue("$sid",SchoolId); cmd.Parameters.AddWithValue("$q",search.Trim());
        using var r=cmd.ExecuteReader(); var list=new List<CaseProfile>();
        while(r.Read()) list.Add(new CaseProfile{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),OpenedAt=ParseDay(r.GetString(4)),Status=r.GetString(5),Category=r.GetString(6),Summary=r.GetString(7),TeacherId=r.GetString(8),TeacherName=r.GetString(9),Notes=r.GetString(10),SyncStatus=r.GetString(11),RecoveredAt=ParseNullableDay(r.GetString(12)),RecoveryOutcome=r.GetString(13),RecoveryTeacherId=r.GetString(14),RecoveryTeacherName=r.GetString(15),MonitoringUntil=ParseNullableDay(r.GetString(16)),RecoveryNotes=r.GetString(17)});
        return list;
    }

    public void SaveCaseProfile(CaseProfile x)
    {
        using var c=OpenConnection(); var now=NowIso(); if(string.IsNullOrWhiteSpace(x.Id)) x.Id=Guid.NewGuid().ToString();
        using var cmd=c.CreateCommand(); cmd.CommandText=@"INSERT INTO case_profiles(id,school_id,student_id,opened_at,status,category,summary,teacher_id,notes,created_at,updated_at,version,sync_status)
VALUES($id,$sid,$student,$date,$status,$category,$summary,$teacher,$notes,$now,$now,1,'pending')
ON CONFLICT(id) DO UPDATE SET student_id=$student,opened_at=$date,status=$status,category=$category,summary=$summary,teacher_id=$teacher,notes=$notes,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";
        AddCommon(cmd,x.Id,x.StudentId,x.OpenedAt,now); cmd.Parameters.AddWithValue("$status",x.Status); cmd.Parameters.AddWithValue("$category",x.Category); cmd.Parameters.AddWithValue("$summary",x.Summary.Trim()); cmd.Parameters.AddWithValue("$teacher",x.TeacherId); cmd.Parameters.AddWithValue("$notes",x.Notes.Trim()); cmd.ExecuteNonQuery();
    }
    public void DeleteCaseProfile(string id)=>SoftDelete("case_profiles",id);

    // MARK: Dipulihkan v0.6
    public List<CaseProfile> GetRecoveryCases(string search="", string filter="SEMUA")
    {
        var all=GetCaseProfiles(search);
        return filter switch
        {
            "AKTIF" => all.Where(x=>x.Status=="AKTIF").ToList(),
            "PEMANTAUAN" => all.Where(x=>x.Status=="PEMANTAUAN").ToList(),
            "DIPULIHKAN" => all.Where(x=>x.Status=="SELESAI").ToList(),
            _ => all
        };
    }

    public (int Active,int Monitoring,int Recovered) GetRecoverySummary()
    {
        using var c=OpenConnection();
        int Count(string status)
        {
            using var cmd=c.CreateCommand();
            cmd.CommandText="SELECT COUNT(*) FROM case_profiles WHERE school_id=$sid AND deleted_at IS NULL AND status=$status";
            cmd.Parameters.AddWithValue("$sid",SchoolId);
            cmd.Parameters.AddWithValue("$status",status);
            return Convert.ToInt32(cmd.ExecuteScalar()??0);
        }
        return (Count("AKTIF"),Count("PEMANTAUAN"),Count("SELESAI"));
    }

    public int MarkCaseRecovered(string caseId,DateTime recoveredAt,string outcome,string teacherId,DateTime? monitoringUntil,string notes)
    {
        using var c=OpenConnection();var now=NowIso();using var cmd=c.CreateCommand();
        cmd.CommandText=@"UPDATE case_profiles SET status='SELESAI',recovered_at=$recovered,recovery_outcome=$outcome,recovery_teacher_id=$teacher,monitoring_until=$monitor,recovery_notes=$notes,updated_at=$now,version=version+1,sync_status='pending' WHERE id=$id AND school_id=$sid AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue("$recovered",recoveredAt.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$outcome",outcome.Trim());
        cmd.Parameters.AddWithValue("$teacher",teacherId??"");
        cmd.Parameters.AddWithValue("$monitor",monitoringUntil is null?DBNull.Value:monitoringUntil.Value.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$notes",notes.Trim());
        cmd.Parameters.AddWithValue("$now",now);cmd.Parameters.AddWithValue("$id",caseId);cmd.Parameters.AddWithValue("$sid",SchoolId);
        return cmd.ExecuteNonQuery();
    }

    public void ReturnCaseToMonitoring(string caseId,DateTime? monitoringUntil,string notes)
    {
        using var c=OpenConnection();var now=NowIso();using var cmd=c.CreateCommand();
        cmd.CommandText=@"UPDATE case_profiles SET status='PEMANTAUAN',monitoring_until=$monitor,recovery_notes=$notes,updated_at=$now,version=version+1,sync_status='pending' WHERE id=$id AND school_id=$sid AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue("$monitor",monitoringUntil is null?DBNull.Value:monitoringUntil.Value.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$notes",notes.Trim());cmd.Parameters.AddWithValue("$now",now);cmd.Parameters.AddWithValue("$id",caseId);cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.ExecuteNonQuery();
    }

    public List<GuardianContact> GetGuardianContacts(string search="")
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT g.id,g.student_id,s.nama,s.kelas,g.contact_date,g.method,g.guardian_name,g.phone,g.outcome,g.notes,g.sync_status
FROM guardian_contacts g JOIN students s ON s.id=g.student_id
WHERE g.school_id=$sid AND g.deleted_at IS NULL AND ($q='' OR lower(s.nama||' '||s.kelas||' '||g.guardian_name||' '||g.method||' '||g.outcome) LIKE '%'||lower($q)||'%')
ORDER BY g.contact_date DESC,s.nama";cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$q",search.Trim());using var r=cmd.ExecuteReader();var list=new List<GuardianContact>();while(r.Read())list.Add(new GuardianContact{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),ContactDate=ParseDay(r.GetString(4)),Method=r.GetString(5),GuardianName=r.GetString(6),Phone=r.GetString(7),Outcome=r.GetString(8),Notes=r.GetString(9),SyncStatus=r.GetString(10)});return list;
    }
    public void SaveGuardianContact(GuardianContact x)
    {
        using var c=OpenConnection();var now=NowIso();if(string.IsNullOrWhiteSpace(x.Id))x.Id=Guid.NewGuid().ToString();using var cmd=c.CreateCommand();cmd.CommandText=@"INSERT INTO guardian_contacts(id,school_id,student_id,contact_date,method,guardian_name,phone,outcome,notes,created_at,updated_at,version,sync_status) VALUES($id,$sid,$student,$date,$method,$guardian,$phone,$outcome,$notes,$now,$now,1,'pending') ON CONFLICT(id) DO UPDATE SET student_id=$student,contact_date=$date,method=$method,guardian_name=$guardian,phone=$phone,outcome=$outcome,notes=$notes,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";AddCommon(cmd,x.Id,x.StudentId,x.ContactDate,now);cmd.Parameters.AddWithValue("$method",x.Method);cmd.Parameters.AddWithValue("$guardian",x.GuardianName.Trim());cmd.Parameters.AddWithValue("$phone",x.Phone.Trim());cmd.Parameters.AddWithValue("$outcome",x.Outcome.Trim());cmd.Parameters.AddWithValue("$notes",x.Notes.Trim());cmd.ExecuteNonQuery();
    }
    public void DeleteGuardianContact(string id)=>SoftDelete("guardian_contacts",id);

    public List<HomeVisit> GetHomeVisits(string search="")
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT v.id,v.student_id,s.nama,s.kelas,v.visit_date,v.teacher_id,COALESCE(t.nama,''),v.address,v.outcome,v.follow_up,v.notes,v.sync_status FROM home_visits v JOIN students s ON s.id=v.student_id LEFT JOIN teachers t ON t.id=v.teacher_id AND t.deleted_at IS NULL WHERE v.school_id=$sid AND v.deleted_at IS NULL AND ($q='' OR lower(s.nama||' '||s.kelas||' '||v.outcome||' '||v.follow_up) LIKE '%'||lower($q)||'%') ORDER BY v.visit_date DESC,s.nama";cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$q",search.Trim());using var r=cmd.ExecuteReader();var list=new List<HomeVisit>();while(r.Read())list.Add(new HomeVisit{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),VisitDate=ParseDay(r.GetString(4)),TeacherId=r.GetString(5),TeacherName=r.GetString(6),Address=r.GetString(7),Outcome=r.GetString(8),FollowUp=r.GetString(9),Notes=r.GetString(10),SyncStatus=r.GetString(11)});return list;
    }
    public void SaveHomeVisit(HomeVisit x)
    {
        using var c=OpenConnection();var now=NowIso();if(string.IsNullOrWhiteSpace(x.Id))x.Id=Guid.NewGuid().ToString();using var cmd=c.CreateCommand();cmd.CommandText=@"INSERT INTO home_visits(id,school_id,student_id,visit_date,teacher_id,address,outcome,follow_up,notes,created_at,updated_at,version,sync_status) VALUES($id,$sid,$student,$date,$teacher,$address,$outcome,$follow,$notes,$now,$now,1,'pending') ON CONFLICT(id) DO UPDATE SET student_id=$student,visit_date=$date,teacher_id=$teacher,address=$address,outcome=$outcome,follow_up=$follow,notes=$notes,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";AddCommon(cmd,x.Id,x.StudentId,x.VisitDate,now);cmd.Parameters.AddWithValue("$teacher",x.TeacherId);cmd.Parameters.AddWithValue("$address",x.Address.Trim());cmd.Parameters.AddWithValue("$outcome",x.Outcome.Trim());cmd.Parameters.AddWithValue("$follow",x.FollowUp.Trim());cmd.Parameters.AddWithValue("$notes",x.Notes.Trim());cmd.ExecuteNonQuery();
    }
    public void DeleteHomeVisit(string id)=>SoftDelete("home_visits",id);

    public List<CounsellingSession> GetCounsellingSessions(string search="")
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT q.id,q.student_id,s.nama,s.kelas,q.session_date,q.teacher_id,COALESCE(t.nama,''),q.session_type,q.summary,q.next_action,q.notes,q.sync_status FROM counselling_sessions q JOIN students s ON s.id=q.student_id LEFT JOIN teachers t ON t.id=q.teacher_id AND t.deleted_at IS NULL WHERE q.school_id=$sid AND q.deleted_at IS NULL AND ($search='' OR lower(s.nama||' '||s.kelas||' '||q.session_type||' '||q.summary||' '||q.next_action) LIKE '%'||lower($search)||'%') ORDER BY q.session_date DESC,s.nama";cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$search",search.Trim());using var r=cmd.ExecuteReader();var list=new List<CounsellingSession>();while(r.Read())list.Add(new CounsellingSession{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),SessionDate=ParseDay(r.GetString(4)),TeacherId=r.GetString(5),TeacherName=r.GetString(6),SessionType=r.GetString(7),Summary=r.GetString(8),NextAction=r.GetString(9),Notes=r.GetString(10),SyncStatus=r.GetString(11)});return list;
    }
    public void SaveCounsellingSession(CounsellingSession x)
    {
        using var c=OpenConnection();var now=NowIso();if(string.IsNullOrWhiteSpace(x.Id))x.Id=Guid.NewGuid().ToString();using var cmd=c.CreateCommand();cmd.CommandText=@"INSERT INTO counselling_sessions(id,school_id,student_id,session_date,teacher_id,session_type,summary,next_action,notes,created_at,updated_at,version,sync_status) VALUES($id,$sid,$student,$date,$teacher,$type,$summary,$next,$notes,$now,$now,1,'pending') ON CONFLICT(id) DO UPDATE SET student_id=$student,session_date=$date,teacher_id=$teacher,session_type=$type,summary=$summary,next_action=$next,notes=$notes,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";AddCommon(cmd,x.Id,x.StudentId,x.SessionDate,now);cmd.Parameters.AddWithValue("$teacher",x.TeacherId);cmd.Parameters.AddWithValue("$type",x.SessionType);cmd.Parameters.AddWithValue("$summary",x.Summary.Trim());cmd.Parameters.AddWithValue("$next",x.NextAction.Trim());cmd.Parameters.AddWithValue("$notes",x.Notes.Trim());cmd.ExecuteNonQuery();
    }
    public void DeleteCounsellingSession(string id)=>SoftDelete("counselling_sessions",id);

    public List<InterventionRecord> GetInterventions(string search="")
    {
        using var c=OpenConnection();using var cmd=c.CreateCommand();cmd.CommandText=@"SELECT i.id,i.student_id,s.nama,s.kelas,i.intervention_date,i.intervention_type,i.action,i.teacher_id,COALESCE(t.nama,''),i.target_date,i.status,i.result,i.notes,i.sync_status FROM interventions i JOIN students s ON s.id=i.student_id LEFT JOIN teachers t ON t.id=i.teacher_id AND t.deleted_at IS NULL WHERE i.school_id=$sid AND i.deleted_at IS NULL AND ($q='' OR lower(s.nama||' '||s.kelas||' '||i.intervention_type||' '||i.status||' '||i.action) LIKE '%'||lower($q)||'%') ORDER BY i.intervention_date DESC,s.nama";cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$q",search.Trim());using var r=cmd.ExecuteReader();var list=new List<InterventionRecord>();while(r.Read())list.Add(new InterventionRecord{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),InterventionDate=ParseDay(r.GetString(4)),InterventionType=r.GetString(5),Action=r.GetString(6),TeacherId=r.GetString(7),TeacherName=r.GetString(8),TargetDate=r.IsDBNull(9)?null:ParseDay(r.GetString(9)),Status=r.GetString(10),Result=r.GetString(11),Notes=r.GetString(12),SyncStatus=r.GetString(13)});return list;
    }
    public void SaveIntervention(InterventionRecord x)
    {
        using var c=OpenConnection();var now=NowIso();if(string.IsNullOrWhiteSpace(x.Id))x.Id=Guid.NewGuid().ToString();using var cmd=c.CreateCommand();cmd.CommandText=@"INSERT INTO interventions(id,school_id,student_id,intervention_date,intervention_type,action,teacher_id,target_date,status,result,notes,created_at,updated_at,version,sync_status) VALUES($id,$sid,$student,$date,$type,$action,$teacher,$target,$status,$result,$notes,$now,$now,1,'pending') ON CONFLICT(id) DO UPDATE SET student_id=$student,intervention_date=$date,intervention_type=$type,action=$action,teacher_id=$teacher,target_date=$target,status=$status,result=$result,notes=$notes,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";AddCommon(cmd,x.Id,x.StudentId,x.InterventionDate,now);cmd.Parameters.AddWithValue("$type",x.InterventionType);cmd.Parameters.AddWithValue("$action",x.Action.Trim());cmd.Parameters.AddWithValue("$teacher",x.TeacherId);cmd.Parameters.AddWithValue("$target",x.TargetDate is null?DBNull.Value:x.TargetDate.Value.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$status",x.Status);cmd.Parameters.AddWithValue("$result",x.Result.Trim());cmd.Parameters.AddWithValue("$notes",x.Notes.Trim());cmd.ExecuteNonQuery();
    }
    public void DeleteIntervention(string id)=>SoftDelete("interventions",id);


    // MARK: Evidens v0.5
    public List<EvidenceFileRecord> GetEvidenceFiles(string search="")
    {
        using var c=OpenConnection(); using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT e.id,e.student_id,s.nama,s.kelas,e.evidence_date,e.evidence_type,e.title,e.file_name,e.mime_type,e.file_size,e.notes,e.sync_status
FROM evidence_files e JOIN students s ON s.id=e.student_id
WHERE e.school_id=$sid AND e.deleted_at IS NULL
AND ($q='' OR lower(s.nama||' '||s.kelas||' '||e.evidence_type||' '||e.title||' '||e.file_name||' '||e.notes) LIKE '%'||lower($q)||'%')
ORDER BY e.evidence_date DESC,e.updated_at DESC";
        cmd.Parameters.AddWithValue("$sid",SchoolId); cmd.Parameters.AddWithValue("$q",search.Trim());
        using var r=cmd.ExecuteReader(); var list=new List<EvidenceFileRecord>();
        while(r.Read()) list.Add(new EvidenceFileRecord{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),EvidenceDate=ParseDay(r.GetString(4)),EvidenceType=r.GetString(5),Title=r.GetString(6),FileName=r.GetString(7),MimeType=r.GetString(8),FileSize=r.GetInt64(9),Notes=r.GetString(10),SyncStatus=r.GetString(11)});
        return list;
    }

    public EvidenceFileRecord? GetEvidenceFile(string id)
    {
        using var c=OpenConnection(); using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT e.id,e.student_id,s.nama,s.kelas,e.evidence_date,e.evidence_type,e.title,e.file_name,e.mime_type,e.file_size,e.notes,e.file_data,e.sync_status
FROM evidence_files e JOIN students s ON s.id=e.student_id
WHERE e.id=$id AND e.school_id=$sid AND e.deleted_at IS NULL LIMIT 1";
        cmd.Parameters.AddWithValue("$id",id); cmd.Parameters.AddWithValue("$sid",SchoolId); using var r=cmd.ExecuteReader();
        if(!r.Read()) return null;
        return new EvidenceFileRecord{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),EvidenceDate=ParseDay(r.GetString(4)),EvidenceType=r.GetString(5),Title=r.GetString(6),FileName=r.GetString(7),MimeType=r.GetString(8),FileSize=r.GetInt64(9),Notes=r.GetString(10),FileData=(byte[])r[11],SyncStatus=r.GetString(12)};
    }

    public void SaveEvidenceFile(EvidenceFileRecord x)
    {
        if(x.FileData.Length==0) throw new InvalidOperationException("Fail evidens belum dipilih.");
        using var c=OpenConnection(); var now=NowIso(); if(string.IsNullOrWhiteSpace(x.Id)) x.Id=Guid.NewGuid().ToString(); using var cmd=c.CreateCommand();
        cmd.CommandText=@"INSERT INTO evidence_files(id,school_id,student_id,evidence_date,evidence_type,title,file_name,mime_type,file_size,notes,file_data,created_at,updated_at,version,sync_status)
VALUES($id,$sid,$student,$date,$type,$title,$name,$mime,$size,$notes,$data,$now,$now,1,'pending')
ON CONFLICT(id) DO UPDATE SET student_id=$student,evidence_date=$date,evidence_type=$type,title=$title,file_name=$name,mime_type=$mime,file_size=$size,notes=$notes,file_data=$data,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";
        AddCommon(cmd,x.Id,x.StudentId,x.EvidenceDate,now); cmd.Parameters.AddWithValue("$type",x.EvidenceType); cmd.Parameters.AddWithValue("$title",x.Title.Trim()); cmd.Parameters.AddWithValue("$name",x.FileName); cmd.Parameters.AddWithValue("$mime",x.MimeType); cmd.Parameters.AddWithValue("$size",x.FileData.LongLength); cmd.Parameters.AddWithValue("$notes",x.Notes.Trim()); cmd.Parameters.AddWithValue("$data",x.FileData); cmd.ExecuteNonQuery(); x.FileSize=x.FileData.LongLength;
    }
    public void DeleteEvidenceFile(string id)=>SoftDelete("evidence_files",id);

    public List<VideoEvidenceRecord> GetVideoEvidence(string search="")
    {
        using var c=OpenConnection(); using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT v.id,v.student_id,s.nama,s.kelas,v.evidence_date,v.provider,v.source_url,v.source_id,v.title,v.notes,v.sync_status
FROM video_evidence v JOIN students s ON s.id=v.student_id
WHERE v.school_id=$sid AND v.deleted_at IS NULL
AND ($q='' OR lower(s.nama||' '||s.kelas||' '||v.provider||' '||v.source_url||' '||v.title||' '||v.notes) LIKE '%'||lower($q)||'%')
ORDER BY v.evidence_date DESC,v.updated_at DESC";
        cmd.Parameters.AddWithValue("$sid",SchoolId); cmd.Parameters.AddWithValue("$q",search.Trim()); using var r=cmd.ExecuteReader(); var list=new List<VideoEvidenceRecord>();
        while(r.Read()) list.Add(new VideoEvidenceRecord{Id=r.GetString(0),StudentId=r.GetString(1),StudentName=r.GetString(2),Kelas=r.GetString(3),EvidenceDate=ParseDay(r.GetString(4)),Provider=r.GetString(5),SourceUrl=r.GetString(6),SourceId=r.GetString(7),Title=r.GetString(8),Notes=r.GetString(9),SyncStatus=r.GetString(10)});
        return list;
    }

    public void SaveVideoEvidence(VideoEvidenceRecord x)
    {
        using var c=OpenConnection(); var now=NowIso(); if(string.IsNullOrWhiteSpace(x.Id)) x.Id=Guid.NewGuid().ToString(); using var cmd=c.CreateCommand();
        cmd.CommandText=@"INSERT INTO video_evidence(id,school_id,student_id,evidence_date,provider,source_url,source_id,title,notes,created_at,updated_at,version,sync_status)
VALUES($id,$sid,$student,$date,$provider,$url,$source,$title,$notes,$now,$now,1,'pending')
ON CONFLICT(id) DO UPDATE SET student_id=$student,evidence_date=$date,provider=$provider,source_url=$url,source_id=$source,title=$title,notes=$notes,deleted_at=NULL,updated_at=$now,version=version+1,sync_status='pending'";
        AddCommon(cmd,x.Id,x.StudentId,x.EvidenceDate,now); cmd.Parameters.AddWithValue("$provider",x.Provider); cmd.Parameters.AddWithValue("$url",x.SourceUrl.Trim()); cmd.Parameters.AddWithValue("$source",x.SourceId); cmd.Parameters.AddWithValue("$title",x.Title.Trim()); cmd.Parameters.AddWithValue("$notes",x.Notes.Trim()); cmd.ExecuteNonQuery();
    }
    public void DeleteVideoEvidence(string id)=>SoftDelete("video_evidence",id);

    private static DateTime ParseDay(string value)=>DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?d:DateTime.Today;
    private static DateTime? ParseNullableDay(string value)=>DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?d:null;
    private static string NowIso()=>DateTime.UtcNow.ToString(Iso,CultureInfo.InvariantCulture);
    private void AddCommon(SqliteCommand cmd,string id,string studentId,DateTime date,string now)
    {
        cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$sid",SchoolId);cmd.Parameters.AddWithValue("$student",studentId);cmd.Parameters.AddWithValue("$date",date.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("$now",now);
    }


    public List<PendingSyncStat> GetPendingSyncByTable()
    {
        using var c = OpenConnection();
        var list = new List<PendingSyncStat>();
        foreach (var table in SyncTables)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE school_id=$s AND sync_status='pending'";
            cmd.Parameters.AddWithValue("$s", SchoolId);
            var count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            if (count == 0) continue;
            list.Add(new PendingSyncStat
            {
                TableName = table,
                Label = SyncTableLabel(table),
                Count = count
            });
        }
        return list;
    }

    public List<SyncConflictRow> GetOpenSyncConflicts(int limit = 50)
    {
        using var c = OpenConnection();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT id,table_name,record_id,local_version,server_version,reason,created_at
            FROM sync_conflicts
            WHERE school_id=$s AND resolved=0
            ORDER BY created_at DESC
            LIMIT $l
            """;
        cmd.Parameters.AddWithValue("$s", SchoolId);
        cmd.Parameters.AddWithValue("$l", Math.Max(1, Math.Min(limit, 500)));
        using var r = cmd.ExecuteReader();
        var list = new List<SyncConflictRow>();
        while (r.Read())
        {
            var table = r.GetString(1);
            list.Add(new SyncConflictRow
            {
                Id = r.GetString(0),
                TableName = table,
                Label = SyncTableLabel(table),
                RecordId = r.GetString(2),
                LocalVersion = r.GetInt32(3),
                ServerVersion = r.GetInt32(4),
                Reason = r.GetString(5),
                CreatedAt = r.GetString(6)
            });
        }
        return list;
    }

    private static string SyncTableLabel(string table) => table switch
    {
        "schools" => "Sekolah",
        "students" => "Murid",
        "teachers" => "Guru",
        "calendar_days" => "Takwim",
        "attendance" => "Kehadiran",
        "case_profiles" => "Profil Kes",
        "guardian_contacts" => "Hubungan Penjaga",
        "home_visits" => "Lawatan Rumah",
        "counselling_sessions" => "Kaunseling",
        "interventions" => "Intervensi",
        "warning_letters" => "Surat Amaran",
        "evidence_files" => "Evidens",
        "video_evidence" => "Eviden Video",
        _ => table
    };

    public static readonly string[] SyncTables=["schools","students","teachers","calendar_days","attendance","case_profiles","guardian_contacts","home_visits","counselling_sessions","interventions","warning_letters","evidence_files","video_evidence"];

    private const string MigrationSql = @"
CREATE TABLE IF NOT EXISTS schools (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,name TEXT NOT NULL DEFAULT 'SEKOLAH',code TEXT NOT NULL DEFAULT '',active INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending');
CREATE TABLE IF NOT EXISTS students (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,no_murid TEXT NOT NULL,nama TEXT NOT NULL,kelas TEXT NOT NULL,tingkatan TEXT NOT NULL DEFAULT '',penjaga TEXT NOT NULL DEFAULT '',telefon TEXT NOT NULL DEFAULT '',alamat TEXT NOT NULL DEFAULT '',aktif INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',UNIQUE(school_id,no_murid));
CREATE INDEX IF NOT EXISTS idx_students_school_nama ON students(school_id,nama); CREATE INDEX IF NOT EXISTS idx_students_school_kelas ON students(school_id,kelas);
CREATE TABLE IF NOT EXISTS teachers (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,nama TEXT NOT NULL,jawatan TEXT NOT NULL DEFAULT 'Guru',telefon TEXT NOT NULL DEFAULT '',aktif INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending');
CREATE TABLE IF NOT EXISTS calendar_days (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,tarikh TEXT NOT NULL,jenis TEXT NOT NULL DEFAULT 'Hari Persekolahan',tajuk TEXT NOT NULL DEFAULT '',hari_persekolahan INTEGER NOT NULL DEFAULT 1,minggu_akademik INTEGER,catatan TEXT NOT NULL DEFAULT '',sumber TEXT NOT NULL DEFAULT 'MANUAL',created_by TEXT,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',UNIQUE(school_id,tarikh));
CREATE TABLE IF NOT EXISTS attendance (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,tarikh TEXT NOT NULL,status TEXT NOT NULL DEFAULT '',kategori TEXT NOT NULL DEFAULT '',sebab TEXT NOT NULL DEFAULT '',catatan TEXT NOT NULL DEFAULT '',created_by TEXT,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',UNIQUE(school_id,student_id,tarikh),FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS case_profiles (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,opened_at TEXT NOT NULL,status TEXT NOT NULL DEFAULT 'AKTIF',category TEXT NOT NULL DEFAULT 'PONTENG',summary TEXT NOT NULL DEFAULT '',teacher_id TEXT NOT NULL DEFAULT '',notes TEXT NOT NULL DEFAULT '',recovered_at TEXT,recovery_outcome TEXT NOT NULL DEFAULT '',recovery_teacher_id TEXT NOT NULL DEFAULT '',monitoring_until TEXT,recovery_notes TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS guardian_contacts (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,contact_date TEXT NOT NULL,method TEXT NOT NULL DEFAULT 'TELEFON',guardian_name TEXT NOT NULL DEFAULT '',phone TEXT NOT NULL DEFAULT '',outcome TEXT NOT NULL DEFAULT '',notes TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS home_visits (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,visit_date TEXT NOT NULL,teacher_id TEXT NOT NULL DEFAULT '',address TEXT NOT NULL DEFAULT '',outcome TEXT NOT NULL DEFAULT '',follow_up TEXT NOT NULL DEFAULT '',notes TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS counselling_sessions (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,session_date TEXT NOT NULL,teacher_id TEXT NOT NULL DEFAULT '',session_type TEXT NOT NULL DEFAULT 'INDIVIDU',summary TEXT NOT NULL DEFAULT '',next_action TEXT NOT NULL DEFAULT '',notes TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS interventions (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,intervention_date TEXT NOT NULL,intervention_type TEXT NOT NULL DEFAULT 'KEHADIRAN',action TEXT NOT NULL DEFAULT '',teacher_id TEXT NOT NULL DEFAULT '',target_date TEXT,status TEXT NOT NULL DEFAULT 'DIRANCANG',result TEXT NOT NULL DEFAULT '',notes TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS warning_letters (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,issue_date TEXT NOT NULL,warning_type TEXT NOT NULL DEFAULT 'AMARAN 1',reference_no TEXT NOT NULL DEFAULT '',guardian_name TEXT NOT NULL DEFAULT '',guardian_address TEXT NOT NULL DEFAULT '',total_absences INTEGER NOT NULL DEFAULT 0,title TEXT NOT NULL DEFAULT '',body TEXT NOT NULL DEFAULT '',notes TEXT NOT NULL DEFAULT '',pdf_data BLOB,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS evidence_files (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,evidence_date TEXT NOT NULL,evidence_type TEXT NOT NULL DEFAULT 'DOKUMEN',title TEXT NOT NULL DEFAULT '',file_name TEXT NOT NULL DEFAULT '',mime_type TEXT NOT NULL DEFAULT 'application/octet-stream',file_size INTEGER NOT NULL DEFAULT 0,notes TEXT NOT NULL DEFAULT '',file_data BLOB NOT NULL,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS video_evidence (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,student_id TEXT NOT NULL,evidence_date TEXT NOT NULL,provider TEXT NOT NULL DEFAULT 'UNKNOWN',source_url TEXT NOT NULL DEFAULT '',source_id TEXT NOT NULL DEFAULT '',title TEXT NOT NULL DEFAULT '',notes TEXT NOT NULL DEFAULT '',created_at TEXT NOT NULL,updated_at TEXT NOT NULL,deleted_at TEXT,version INTEGER NOT NULL DEFAULT 1,sync_status TEXT NOT NULL DEFAULT 'pending',FOREIGN KEY(student_id) REFERENCES students(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY,value TEXT NOT NULL DEFAULT ''); CREATE TABLE IF NOT EXISTS sync_state (key TEXT PRIMARY KEY,value TEXT NOT NULL DEFAULT '');
CREATE TABLE IF NOT EXISTS sync_conflicts (id TEXT PRIMARY KEY,school_id TEXT NOT NULL,table_name TEXT NOT NULL,record_id TEXT NOT NULL,local_version INTEGER NOT NULL DEFAULT 0,server_version INTEGER NOT NULL DEFAULT 0,reason TEXT NOT NULL DEFAULT '',server_payload TEXT NOT NULL DEFAULT '{}',created_at TEXT NOT NULL,resolved INTEGER NOT NULL DEFAULT 0);
";
}
