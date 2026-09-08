using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SistemPengurusanKehadiran.Services;

public sealed class ImportSummary
{
    public int Murid { get; set; }
    public int Takwim { get; set; }
    public int Kehadiran { get; set; }
    public int Skipped { get; set; }
    public int NeedsReview { get; set; }
    public override string ToString() => $"Murid {Murid} • Takwim {Takwim} • Kehadiran {Kehadiran} • Skip {Skipped}" + (NeedsReview > 0 ? $" • Semakan {NeedsReview}" : "");
}

public sealed class UniversalZipImportService
{
    public static UniversalZipImportService Instance { get; } = new();

    public ImportSummary Import(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        ValidateArchive(zip);
        var summary = new ImportSummary { NeedsReview = ReadNeedsReview(zip) };

        using var connection = DatabaseService.Instance.OpenConnection();
        using var transaction = connection.BeginTransaction();

        ImportStudents(zip, connection, transaction, summary);
        ImportCalendar(zip, connection, transaction, summary);
        ImportAttendance(zip, connection, transaction, summary);

        transaction.Commit();
        return summary;
    }

    private static void ValidateArchive(ZipArchive zip)
    {
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Contains("../", StringComparison.Ordinal) || name.Contains(":/", StringComparison.Ordinal))
                throw new InvalidDataException("ZIP mengandungi laluan yang tidak selamat.");
        }

        var manifest = zip.GetEntry("manifest.json");
        if (manifest == null)
            throw new InvalidDataException("manifest.json tidak ditemui. Gunakan Fail Ponteng Import Package v1.");

        using var stream = manifest.Open();
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var packageType = root.TryGetProperty("package_type", out var type) ? type.GetString() : null;
        if (!string.Equals(packageType, "fail_ponteng_import", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("package_type ZIP tidak dikenali.");

        var hasModule = zip.Entries.Any(x =>
            (x.FullName.StartsWith("murid/", StringComparison.OrdinalIgnoreCase) ||
             x.FullName.StartsWith("takwim/", StringComparison.OrdinalIgnoreCase) ||
             x.FullName.StartsWith("kehadiran/", StringComparison.OrdinalIgnoreCase)) &&
            x.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        if (!hasModule) throw new InvalidDataException("ZIP tidak mempunyai data Murid, Takwim atau Kehadiran.");
    }

    private static int ReadNeedsReview(ZipArchive zip)
    {
        var entry = zip.GetEntry("validation.json");
        if (entry == null) return 0;
        try
        {
            using var stream = entry.Open();
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("needs_review", out var review) && review.ValueKind == JsonValueKind.Array)
                return review.GetArrayLength();
        }
        catch { }
        return 0;
    }

    private static void ImportStudents(ZipArchive zip, SqliteConnection c, SqliteTransaction tx, ImportSummary sum)
    {
        foreach (var entry in zip.Entries.Where(x => x.FullName.StartsWith("murid/", StringComparison.OrdinalIgnoreCase) && x.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            foreach (var row in CsvUtil.Read(stream))
            {
                var no = V(row, "no_murid", "no._kp", "nokp", "id");
                var nama = V(row, "nama", "nama_murid");
                var kelas = V(row, "kelas");
                if (string.IsNullOrWhiteSpace(no) || string.IsNullOrWhiteSpace(nama) || string.IsNullOrWhiteSpace(kelas)) { sum.Skipped++; continue; }
                UpsertStudent(c, tx, no, nama, kelas, V(row, "tingkatan"), V(row, "penjaga", "nama_penjaga"), V(row, "telefon", "no_telefon"), V(row, "alamat", "address"));
                sum.Murid++;
            }
        }
    }

    private static void ImportCalendar(ZipArchive zip, SqliteConnection c, SqliteTransaction tx, ImportSummary sum)
    {
        foreach (var entry in zip.Entries.Where(x => x.FullName.StartsWith("takwim/", StringComparison.OrdinalIgnoreCase) && x.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            foreach (var row in CsvUtil.Read(stream))
            {
                if (!DateTime.TryParseExact(V(row, "tarikh"), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { sum.Skipped++; continue; }
                using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO calendar_days(id,school_id,tarikh,jenis,tajuk,hari_persekolahan,minggu_akademik,catatan,sumber,created_at,updated_at,version,sync_status) VALUES($id,$sid,$d,$j,$t,$h,$m,$c,'ZIP',$n,$n,1,'pending') ON CONFLICT(school_id,tarikh) DO UPDATE SET jenis=$j,tajuk=$t,hari_persekolahan=$h,minggu_akademik=$m,catatan=$c,updated_at=$n,version=version+1,sync_status='pending'";
                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("$sid", DatabaseService.Instance.SchoolId);
                cmd.Parameters.AddWithValue("$d", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("$j", V(row, "jenis"));
                cmd.Parameters.AddWithValue("$t", V(row, "tajuk"));
                cmd.Parameters.AddWithValue("$h", V(row, "hari_persekolahan") == "1" ? 1 : 0);
                var week = V(row, "minggu_akademik");
                cmd.Parameters.AddWithValue("$m", int.TryParse(week, out var wi) ? wi : DBNull.Value);
                cmd.Parameters.AddWithValue("$c", V(row, "catatan"));
                cmd.Parameters.AddWithValue("$n", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
                sum.Takwim++;
            }
        }
    }

    private static void ImportAttendance(ZipArchive zip, SqliteConnection c, SqliteTransaction tx, ImportSummary sum)
    {
        foreach (var entry in zip.Entries.Where(x => x.FullName.StartsWith("kehadiran/", StringComparison.OrdinalIgnoreCase) && x.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            foreach (var row in CsvUtil.Read(stream))
            {
                var no = V(row, "no_murid");
                if (!DateTime.TryParseExact(V(row, "tarikh"), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || string.IsNullOrWhiteSpace(no)) { sum.Skipped++; continue; }
                var studentId = StudentId(c, tx, no);
                if (studentId == null) { sum.Skipped++; continue; }

                using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO attendance(id,school_id,student_id,tarikh,status,kategori,sebab,catatan,created_at,updated_at,version,sync_status) VALUES($id,$sid,$st,$d,$status,$kat,$sebab,$cat,$n,$n,1,'pending') ON CONFLICT(school_id,student_id,tarikh) DO UPDATE SET status=$status,kategori=$kat,sebab=$sebab,catatan=$cat,updated_at=$n,version=version+1,sync_status='pending'";
                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("$sid", DatabaseService.Instance.SchoolId);
                cmd.Parameters.AddWithValue("$st", studentId);
                cmd.Parameters.AddWithValue("$d", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("$status", NormalizeStatus(V(row, "status")));
                cmd.Parameters.AddWithValue("$kat", V(row, "kategori"));
                cmd.Parameters.AddWithValue("$sebab", V(row, "sebab"));
                cmd.Parameters.AddWithValue("$cat", V(row, "catatan"));
                cmd.Parameters.AddWithValue("$n", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
                sum.Kehadiran++;
            }
        }
    }

    private static string V(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys) if (row.TryGetValue(key, out var value)) return value;
        return "";
    }

    private static string NormalizeStatus(string value) => value.Trim().ToUpperInvariant() switch
    {
        "H" => "HADIR",
        "TH" => "TIDAK HADIR",
        "P" => "PONTENG",
        "SAKIT" => "MC",
        "CUTI" => "CUTI BERSEBAB",
        var x => x
    };

    private static void UpsertStudent(SqliteConnection c, SqliteTransaction tx, string no, string nama, string kelas, string ting, string pen, string tel, string alamat)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO students(id,school_id,no_murid,nama,kelas,tingkatan,penjaga,telefon,alamat,aktif,created_at,updated_at,version,sync_status) VALUES($id,$sid,$no,$nama,$kelas,$ting,$pen,$tel,$alamat,1,$n,$n,1,'pending') ON CONFLICT(school_id,no_murid) DO UPDATE SET nama=$nama,kelas=$kelas,tingkatan=$ting,penjaga=$pen,telefon=$tel,alamat=$alamat,aktif=1,updated_at=$n,version=version+1,sync_status='pending'";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$sid", DatabaseService.Instance.SchoolId);
        cmd.Parameters.AddWithValue("$no", no);
        cmd.Parameters.AddWithValue("$nama", nama.ToUpperInvariant());
        cmd.Parameters.AddWithValue("$kelas", kelas.ToUpperInvariant());
        cmd.Parameters.AddWithValue("$ting", ting.ToUpperInvariant());
        cmd.Parameters.AddWithValue("$pen", pen.ToUpperInvariant());
        cmd.Parameters.AddWithValue("$tel", tel);
        cmd.Parameters.AddWithValue("$alamat", alamat);
        cmd.Parameters.AddWithValue("$n", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private static string? StudentId(SqliteConnection c, SqliteTransaction tx, string no)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM students WHERE school_id=$s AND no_murid=$n AND deleted_at IS NULL LIMIT 1";
        cmd.Parameters.AddWithValue("$s", DatabaseService.Instance.SchoolId);
        cmd.Parameters.AddWithValue("$n", no);
        return cmd.ExecuteScalar()?.ToString();
    }
}
