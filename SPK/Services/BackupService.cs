using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SistemPengurusanKehadiran.Helpers;

namespace SistemPengurusanKehadiran.Services;

public sealed class BackupService
{
    public static BackupService Instance { get; } = new();

    // Standard backup lintas platform ditetapkan berdasarkan format macOS.
    private const string PackageType = "sistem_pengurusan_kehadiran_backup";
    private const string PackageVersion = "1.0";
    private const string CanonicalDatabaseEntry = "database/sistem_pengurusan_kehadiran.sqlite3";
    private const string LegacyWindowsDatabaseEntry = "database/spk.sqlite3";

    public void Create(string zipPath)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"sistem_pengurusan_kehadiran_{Guid.NewGuid():N}.sqlite3");
        try
        {
            // SQLite Online Backup API menghasilkan satu fail DB konsisten walaupun DB aktif dalam mod WAL.
            using (var src = DatabaseService.Instance.OpenConnection())
            using (var dst = new SqliteConnection($"Data Source={tmp}"))
            {
                dst.Open();
                src.BackupDatabase(dst);
                using var q = dst.CreateCommand();
                q.CommandText = "PRAGMA quick_check";
                if ((q.ExecuteScalar()?.ToString() ?? "") != "ok")
                    throw new InvalidDataException("SQLite quick_check gagal");
            }

            if (File.Exists(zipPath)) File.Delete(zipPath);
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            zip.CreateEntry("database/");
            zip.CreateEntryFromFile(tmp, CanonicalDatabaseEntry, CompressionLevel.Optimal);

            WriteJsonEntry(zip, "manifest.json", new
            {
                app_version = "0.7.0",
                created_at = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                database_file = CanonicalDatabaseEntry,
                includes_evidence_blobs = true,
                includes_server_token = false,
                includes_video_cache = false,
                includes_video_metadata = true,
                includes_warning_letter_pdfs = true,
                package_type = PackageType,
                package_version = PackageVersion,
                school_id = DatabaseService.Instance.SchoolId
            });

            WriteJsonEntry(zip, "validation.json", new
            {
                notes = new[]
                {
                    "Video sebenar/cache lokal tidak dimasukkan ke backup.",
                    "Token Railway/FastAPI disimpan dalam storan privat aplikasi dan tidak dimasukkan ke backup."
                },
                server_token_excluded = true,
                sqlite_quick_check = "ok",
                status = "OK",
                video_cache_excluded = true
            });
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public void Restore(string zipPath)
    {
        // Maintenance mode: Auto Sync tidak boleh membuka SQLite ketika fail DB sedang diganti.
        AutoSyncService.Instance.Stop();
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var manifest = ReadManifest(zip);

            // Canonical dahulu. Fallback lama hanya untuk menyelamatkan backup Windows v0.5.2 ke bawah.
            var databaseEntryName = manifest.DatabaseFile;
            var entry = !string.IsNullOrWhiteSpace(databaseEntryName)
                ? zip.GetEntry(databaseEntryName)
                : null;

            entry ??= zip.GetEntry(CanonicalDatabaseEntry);
            entry ??= zip.GetEntry(LegacyWindowsDatabaseEntry);
            if (entry is null)
                throw new InvalidDataException($"{CanonicalDatabaseEntry} tiada");

            var dbPath = DatabaseService.Instance.DatabasePath;
            var safe = dbPath + ".before_restore";
            var tmp = dbPath + ".restore";

            // Ekstrak dan sahkan backup SEBELUM menyentuh database aktif.
            entry.ExtractToFile(tmp, true);
            ValidateSqlite(tmp);

            try
            {
                // Simpan snapshot konsisten database semasa untuk rollback. Ini selamat walaupun SQLite sedang WAL.
                CreateSafetySnapshot(safe);

                // Microsoft.Data.Sqlite menggunakan connection pooling secara lalai. Pool boleh memegang fail
                // SQLite walaupun objek connection telah Dispose. Kosongkan pool sebelum menukar fail database.
                SqliteConnection.ClearAllPools();

                DeleteSidecarsWithRetry(dbPath);
                CopyWithRetry(tmp, dbPath);

                // Pastikan tiada handle lama sebelum membuka database yang baru dipulihkan.
                SqliteConnection.ClearAllPools();
                DatabaseService.Instance.Initialize();
            }
            catch
            {
                // Jika restore gagal, lepaskan semua pooled handle dahulu kemudian pulihkan snapshot asal.
                SqliteConnection.ClearAllPools();
                try
                {
                    DeleteSidecarsWithRetry(dbPath);
                    if (File.Exists(safe))
                    {
                        CopyWithRetry(safe, dbPath);
                        SqliteConnection.ClearAllPools();
                        DatabaseService.Instance.Initialize();
                    }
                }
                catch
                {
                    // Jangan menutup exception restore asal dengan exception rollback.
                }
                throw;
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                TryDelete(tmp);
            }
        }
        finally
        {
            // Baca semula tetapan auto-sync daripada database yang kini aktif (restore atau rollback).
            AutoSyncService.Instance.Start();
        }
    }

    private static void CreateSafetySnapshot(string safePath)
    {
        TryDelete(safePath);
        using var src = DatabaseService.Instance.OpenConnection();
        using var dst = new SqliteConnection($"Data Source={safePath};Pooling=False");
        dst.Open();
        src.BackupDatabase(dst);
    }

    private static void DeleteSidecarsWithRetry(string dbPath)
    {
        foreach (var side in new[] { dbPath + "-wal", dbPath + "-shm" })
        {
            if (!File.Exists(side)) continue;
            FileActionWithRetry(() => File.Delete(side));
        }
    }

    private static void CopyWithRetry(string source, string destination)
        => FileActionWithRetry(() => File.Copy(source, destination, true));

    private static void FileActionWithRetry(Action action)
    {
        IOException? last = null;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException ex)
            {
                last = ex;
                SqliteConnection.ClearAllPools();
                Thread.Sleep(100 + (attempt * 50));
            }
        }
        throw last ?? new IOException("Operasi fail SQLite gagal selepas beberapa percubaan.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Fail sementara akan dibersihkan pada cubaan seterusnya / oleh OS.
        }
    }

    private static BackupManifest ReadManifest(ZipArchive zip)
    {
        var entry = zip.GetEntry("manifest.json");
        if (entry is null)
            return new BackupManifest { DatabaseFile = CanonicalDatabaseEntry };

        using var sr = new StreamReader(entry.Open());
        using var doc = JsonDocument.Parse(sr.ReadToEnd());
        var root = doc.RootElement;

        var manifest = new BackupManifest
        {
            PackageType = GetString(root, "package_type"),
            PackageVersion = GetString(root, "package_version"),
            DatabaseFile = GetString(root, "database_file")
        };

        // Backup Mac standard mesti dikenali. Backup Windows lama masih dibenarkan untuk migrasi sahaja.
        if (!string.IsNullOrWhiteSpace(manifest.PackageType)
            && !string.Equals(manifest.PackageType, PackageType, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(manifest.PackageType, "spk_backup", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("ZIP ini bukan backup Sistem Pengurusan Kehadiran yang sah.");
        }

        if (string.IsNullOrWhiteSpace(manifest.DatabaseFile))
            manifest.DatabaseFile = CanonicalDatabaseEntry;

        return manifest;
    }

    private static string GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static void ValidateSqlite(string path)
    {
        using var c = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        c.Open();
        using var q = c.CreateCommand();
        q.CommandText = "PRAGMA quick_check";
        if ((q.ExecuteScalar()?.ToString() ?? "") != "ok")
            throw new InvalidDataException("Backup SQLite rosak");

        // Jadual asas yang wajib ada dalam format canonical Mac/Windows.
        using var tables = c.CreateCommand();
        tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('settings','students','attendance')";
        if (Convert.ToInt32(tables.ExecuteScalar() ?? 0) < 3)
            throw new InvalidDataException("Struktur database backup tidak serasi.");
    }

    private static void WriteJsonEntry<T>(ZipArchive zip, string name, T value)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var sw = new StreamWriter(entry.Open());
        sw.Write(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class BackupManifest
    {
        public string PackageType { get; set; } = "";
        public string PackageVersion { get; set; } = "";
        public string DatabaseFile { get; set; } = "";
    }
}
