using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SistemPengurusanKehadiran.Models;

namespace SistemPengurusanKehadiran.Services;

public sealed class SyncRunResult
{
    public int Pushed { get; set; }
    public int Pulled { get; set; }
    public int Conflicts { get; set; }
    public long Revision { get; set; }
}

public sealed class RailwaySyncService
{
    public static RailwaySyncService Instance { get; } = new();

    private readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim syncGate = new(1, 1);
    private readonly Dictionary<string, HashSet<string>> writableColumnCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> TestAsync(CancellationToken cancellationToken = default)
    {
        var cfg = ValidateBaseConfig(requireToken: false);
        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            cfg.BaseUrl.TrimEnd('/') + "/health");

        var token = ServerConfigService.Instance.LoadToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var res = await http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)res.StatusCode}: {TrimBody(body)}");
        }

        var suffix = string.IsNullOrWhiteSpace(body) ? "" : " · health OK";
        return $"Server Online · HTTP {(int)res.StatusCode}{suffix}";
    }

    public async Task<SyncRunResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        await syncGate.WaitAsync(cancellationToken);
        try
        {
            var cfg = ValidateBaseConfig(requireToken: true);
            var token = ServerConfigService.Instance.LoadToken()
                ?? throw new InvalidOperationException("Token sync tiada");

            if (!string.Equals(cfg.SchoolId, DatabaseService.Instance.SchoolId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "school_id Server Config tidak sama dengan SQLite lokal");
            }

            var endpoint = cfg.BaseUrl.TrimEnd('/') +
                (cfg.ApiPath.StartsWith('/') ? cfg.ApiPath : "/" + cfg.ApiPath);

            var revision = long.TryParse(
                DatabaseService.Instance.GetSyncState("last_pull_revision"),
                out var rv) ? rv : 0;

            var result = new SyncRunResult { Revision = revision };

            // Satu round = satu batch push. Pull boleh mempunyai beberapa halaman.
            // Had round mengelakkan loop tidak berakhir jika server tidak menerima batch.
            for (var round = 0; round < 80; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pendingBefore = DatabaseService.Instance.PendingSyncCount();
                var push = ReadPending(25);
                var firstPage = true;
                var acceptedThisRound = 0;
                var conflictsThisRound = 0;

                for (var page = 0; page < 50; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var body = new
                    {
                        protocol_version = "1.0",
                        school_id = DatabaseService.Instance.SchoolId,
                        device_id = DeviceId(),
                        last_pull_revision = revision,
                        push = firstPage ? push : new List<SyncRecord>(),
                        pull_limit = 500
                    };

                    using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    req.Content = new StringContent(
                        JsonSerializer.Serialize(body, json),
                        Encoding.UTF8,
                        "application/json");

                    using var resp = await http.SendAsync(req, cancellationToken);
                    var txt = await resp.Content.ReadAsStringAsync(cancellationToken);

                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(
                            $"HTTP {(int)resp.StatusCode}: {TrimBody(txt)}");
                    }

                    var data = JsonSerializer.Deserialize<SyncResponse>(txt, json)
                        ?? throw new InvalidDataException("Respons sync tidak sah");

                    if (!string.Equals(data.ProtocolVersion, "1.0", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Versi protokol sync server tidak serasi");
                    if (data.NextPullRevision < revision)
                        throw new InvalidDataException("Revision server bergerak ke belakang");

                    ValidateResponseTenant(data);
                    MarkAccepted(data.Accepted);
                    RecordConflicts(data.Conflicts);
                    var localConflicts = ApplyChanges(data.Changes);

                    acceptedThisRound += data.Accepted.Count;
                    conflictsThisRound += data.Conflicts.Count + localConflicts;
                    result.Pushed += data.Accepted.Count;
                    result.Pulled += data.Changes.Count;
                    result.Conflicts += data.Conflicts.Count + localConflicts;

                    revision = data.NextPullRevision;
                    result.Revision = revision;
                    DatabaseService.Instance.SetSyncState(
                        "last_pull_revision",
                        revision.ToString());

                    firstPage = false;
                    if (!data.HasMore) break;
                }

                var pendingAfter = DatabaseService.Instance.PendingSyncCount();
                if (pendingAfter == 0) break;

                // Jika tiada rekod berubah, jangan hantar batch sama berulang kali.
                if (push.Count > 0 && pendingAfter >= pendingBefore &&
                    acceptedThisRound == 0 && conflictsThisRound == 0)
                {
                    throw new InvalidOperationException(
                        "Server tidak menerima queue lokal. Sync dihentikan untuk mengelakkan ulangan tanpa henti.");
                }
            }

            DatabaseService.Instance.SetSyncState(
                "last_sync_at",
                DateTime.UtcNow.ToString("o"));
            DatabaseService.Instance.SetSyncState("last_sync_error", "");

            return result;
        }
        catch (Exception ex)
        {
            DatabaseService.Instance.SetSyncState("last_sync_error", ex.Message);
            throw;
        }
        finally
        {
            syncGate.Release();
        }
    }

    private ServerConnectionConfig ValidateBaseConfig(bool requireToken)
    {
        var cfg = ServerConfigService.Instance.Load()
            ?? throw new InvalidOperationException("Import Server Config ZIP dahulu");

        if (!Uri.TryCreate(cfg.BaseUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Base URL mesti HTTPS");
        }

        if (string.IsNullOrWhiteSpace(cfg.ApiPath))
        {
            cfg.ApiPath = "/api/v1/sync";
        }

        if (requireToken && string.IsNullOrWhiteSpace(ServerConfigService.Instance.LoadToken()))
        {
            throw new InvalidOperationException("Token sync tiada");
        }

        if (requireToken && string.IsNullOrWhiteSpace(cfg.SchoolId))
        {
            throw new InvalidOperationException("school_id tiada dalam Server Config");
        }

        return cfg;
    }

    private string DeviceId()
    {
        var id = DatabaseService.Instance.GetSyncState("device_id");
        if (!string.IsNullOrWhiteSpace(id)) return id;

        id = Guid.NewGuid().ToString();
        DatabaseService.Instance.SetSyncState("device_id", id);
        return id;
    }

    private List<SyncRecord> ReadPending(int limit)
    {
        using var c = DatabaseService.Instance.OpenConnection();
        var result = new List<SyncRecord>();

        foreach (var table in DatabaseService.SyncTables)
        {
            if (result.Count >= limit) break;

            using var cmd = c.CreateCommand();
            cmd.CommandText = $"""
                SELECT *
                FROM {table}
                WHERE school_id=$s AND sync_status='pending'
                ORDER BY updated_at
                LIMIT $l
                """;
            cmd.Parameters.AddWithValue("$s", DatabaseService.Instance.SchoolId);
            cmd.Parameters.AddWithValue("$l", limit - result.Count);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var rec = new SyncRecord
                {
                    Table = table,
                    Id = r["id"].ToString()!,
                    SchoolId = r["school_id"].ToString()!,
                    Version = Convert.ToInt32(r["version"]),
                    UpdatedAt = r["updated_at"].ToString()!,
                    DeletedAt = r["deleted_at"] is DBNull ? null : r["deleted_at"].ToString()
                };

                for (var i = 0; i < r.FieldCount; i++)
                {
                    var name = r.GetName(i);
                    if (name is "id" or "school_id" or "version" or
                        "updated_at" or "deleted_at" or "sync_status")
                    {
                        continue;
                    }

                    var value = r.GetValue(i);
                    if (value is DBNull)
                    {
                        rec.Data[name] = null;
                    }
                    else if (value is byte[] bytes)
                    {
                        rec.Data[name] = new Dictionary<string, object>
                        {
                            ["__type"] = "base64",
                            ["data"] = Convert.ToBase64String(bytes)
                        };
                    }
                    else
                    {
                        rec.Data[name] = value;
                    }
                }

                result.Add(rec);
            }
        }

        return result;
    }

    private void MarkAccepted(IEnumerable<SyncAccepted> list)
    {
        using var c = DatabaseService.Instance.OpenConnection();

        foreach (var accepted in list)
        {
            if (!DatabaseService.SyncTables.Contains(accepted.Table)) continue;

            using var cmd = c.CreateCommand();
            cmd.CommandText = $"""
                UPDATE {accepted.Table}
                SET sync_status='synced', version=$v
                WHERE id=$id AND school_id=$s
                """;
            cmd.Parameters.AddWithValue("$v", accepted.Version);
            cmd.Parameters.AddWithValue("$id", accepted.Id);
            cmd.Parameters.AddWithValue("$s", DatabaseService.Instance.SchoolId);
            cmd.ExecuteNonQuery();
        }
    }

    private void RecordConflicts(IEnumerable<SyncConflict> list)
    {
        using var c = DatabaseService.Instance.OpenConnection();

        foreach (var conflict in list)
        {
            if (!DatabaseService.SyncTables.Contains(conflict.Table)) continue;

            var localVersion = 0;
            using (var version = c.CreateCommand())
            {
                version.CommandText = $"SELECT version FROM {conflict.Table} WHERE id=$id AND school_id=$s";
                version.Parameters.AddWithValue("$id", conflict.Id);
                version.Parameters.AddWithValue("$s", DatabaseService.Instance.SchoolId);
                localVersion = Convert.ToInt32(version.ExecuteScalar() ?? 0);
            }

            using (var mark = c.CreateCommand())
            {
                mark.CommandText = $"""
                    UPDATE {conflict.Table}
                    SET sync_status='conflict'
                    WHERE id=$id AND school_id=$s
                    """;
                mark.Parameters.AddWithValue("$id", conflict.Id);
                mark.Parameters.AddWithValue("$s", DatabaseService.Instance.SchoolId);
                mark.ExecuteNonQuery();
            }

            // Elak konflik sama memenuhi jadual setiap kali Auto Sync berjalan.
            using (var old = c.CreateCommand())
            {
                old.CommandText = """
                    UPDATE sync_conflicts
                    SET resolved=1
                    WHERE school_id=$s AND table_name=$t AND record_id=$r AND resolved=0
                    """;
                old.Parameters.AddWithValue("$s", DatabaseService.Instance.SchoolId);
                old.Parameters.AddWithValue("$t", conflict.Table);
                old.Parameters.AddWithValue("$r", conflict.Id);
                old.ExecuteNonQuery();
            }

            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                INSERT INTO sync_conflicts(
                    id, school_id, table_name, record_id,
                    local_version, server_version, reason,
                    server_payload, created_at, resolved)
                VALUES($id,$s,$t,$r,$lv,$sv,$why,$p,$n,0)
                """;
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$s", DatabaseService.Instance.SchoolId);
            cmd.Parameters.AddWithValue("$t", conflict.Table);
            cmd.Parameters.AddWithValue("$r", conflict.Id);
            cmd.Parameters.AddWithValue("$lv", localVersion);
            cmd.Parameters.AddWithValue("$sv", conflict.Server?.Version ?? 0);
            cmd.Parameters.AddWithValue("$why", conflict.Reason);
            cmd.Parameters.AddWithValue("$p", JsonSerializer.Serialize(conflict.Server, json));
            cmd.Parameters.AddWithValue("$n", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    private int ApplyChanges(IEnumerable<SyncRecord> changes)
    {
        using var c = DatabaseService.Instance.OpenConnection();
        var localConflicts = 0;
        var schoolId = DatabaseService.Instance.SchoolId;

        foreach (var record in changes.OrderBy(x => Priority(x.Table)))
        {
            if (!DatabaseService.SyncTables.Contains(record.Table)) continue;

            if (!string.Equals(record.SchoolId, schoolId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Respons sync mengandungi school_id asing pada {record.Table}/{record.Id}");
            }

            // Kekalkan perlindungan asal: perubahan server tidak boleh menimpa rekod
            // yang masih menunggu push atau sudah ditanda konflik pada UUID yang sama.
            var exactStatus = ReadSyncStatus(c, record.Table, record.Id, schoolId);
            if (exactStatus is "pending" or "conflict") continue;

            // UUID boleh berbeza antara peranti walaupun rekod adalah entiti logik yang sama.
            // SQLite Mac/Windows mempunyai UNIQUE natural keys bagi students, calendar_days
            // dan attendance. Jika pull terus INSERT berdasarkan UUID sahaja, SQLite akan
            // melempar UNIQUE constraint failed walaupun data itu sebenarnya rekod sama.
            var natural = FindNaturalKeyMatch(c, record, schoolId);
            var targetId = record.Id;

            if (natural is not null &&
                !string.Equals(natural.Value.Id, record.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (natural.Value.SyncStatus is "pending" or "conflict")
                {
                    MarkLocalNaturalKeyConflict(c, record, natural.Value.Id, schoolId);
                    localConflicts++;
                    continue;
                }

                // Kekalkan UUID lokal bagi rekod natural-key yang sudah wujud. Ini penting
                // khususnya students kerana banyak jadual anak menggunakan student_id.
                // attendance/calendar_days juga selamat dikemas kini pada UUID lokal.
                targetId = natural.Value.Id;
            }

            var allowedColumns = GetWritableColumns(c, record.Table);
            var cols = new List<string>
            {
                "id", "school_id", "version", "updated_at", "deleted_at", "sync_status"
            };
            var vals = new List<object?>
            {
                targetId, record.SchoolId, record.Version, record.UpdatedAt,
                record.DeletedAt, "synced"
            };

            foreach (var kv in record.Data)
            {
                if (!allowedColumns.Contains(kv.Key)) continue;
                cols.Add(kv.Key);
                vals.Add(ToDb(kv.Value));
            }

            var parameters = cols.Select((_, i) => $"$p{i}").ToList();
            var updates = cols.Where(x => x != "id").Select(x => $"{x}=excluded.{x}");

            using var cmd = c.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO {record.Table}({string.Join(',', cols)})
                VALUES({string.Join(',', parameters)})
                ON CONFLICT(id) DO UPDATE SET {string.Join(',', updates)}
                """;

            for (var i = 0; i < vals.Count; i++)
            {
                cmd.Parameters.AddWithValue(parameters[i], vals[i] ?? DBNull.Value);
            }

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19 &&
                ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                // Safety net: sekiranya natural key berubah semasa pull atau ada rekod lama
                // daripada versi terdahulu, jangan biarkan satu constraint menghentikan semua sync.
                var collision = FindNaturalKeyMatch(c, record, schoolId);
                if (collision is not null)
                {
                    MarkLocalNaturalKeyConflict(c, record, collision.Value.Id, schoolId);
                    localConflicts++;
                    continue;
                }

                throw;
            }
        }

        return localConflicts;
    }

    private static string? ReadSyncStatus(
        SqliteConnection connection,
        string table,
        string id,
        string schoolId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT sync_status FROM {table} WHERE id=$id AND school_id=$s LIMIT 1";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$s", schoolId);
        return cmd.ExecuteScalar()?.ToString();
    }

    private static (string Id, string SyncStatus)? FindNaturalKeyMatch(
        SqliteConnection connection,
        SyncRecord record,
        string schoolId)
    {
        string sql;
        var args = new List<(string Name, object Value)>
        {
            ("$s", schoolId)
        };

        switch (record.Table)
        {
            case "attendance":
            {
                var studentId = SyncDataString(record, "student_id");
                var tarikh = SyncDataString(record, "tarikh");
                if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(tarikh)) return null;
                sql = "SELECT id,sync_status FROM attendance WHERE school_id=$s AND student_id=$student AND tarikh=$tarikh LIMIT 1";
                args.Add(("$student", studentId));
                args.Add(("$tarikh", tarikh));
                break;
            }
            case "calendar_days":
            {
                var tarikh = SyncDataString(record, "tarikh");
                if (string.IsNullOrWhiteSpace(tarikh)) return null;
                sql = "SELECT id,sync_status FROM calendar_days WHERE school_id=$s AND tarikh=$tarikh LIMIT 1";
                args.Add(("$tarikh", tarikh));
                break;
            }
            case "students":
            {
                var noMurid = SyncDataString(record, "no_murid");
                if (string.IsNullOrWhiteSpace(noMurid)) return null;
                sql = "SELECT id,sync_status FROM students WHERE school_id=$s AND no_murid=$no LIMIT 1";
                args.Add(("$no", noMurid));
                break;
            }
            default:
                return null;
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg.Name, arg.Value);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader.GetString(0), reader.GetString(1));
    }

    private static string SyncDataString(SyncRecord record, string key)
    {
        if (!record.Data.TryGetValue(key, out var raw)) return "";
        return ToDb(raw)?.ToString() ?? "";
    }

    private void MarkLocalNaturalKeyConflict(
        SqliteConnection connection,
        SyncRecord serverRecord,
        string localId,
        string schoolId)
    {
        var localVersion = 0;
        using (var version = connection.CreateCommand())
        {
            version.CommandText = $"SELECT version FROM {serverRecord.Table} WHERE id=$id AND school_id=$s";
            version.Parameters.AddWithValue("$id", localId);
            version.Parameters.AddWithValue("$s", schoolId);
            localVersion = Convert.ToInt32(version.ExecuteScalar() ?? 0);
        }

        using (var mark = connection.CreateCommand())
        {
            mark.CommandText = $"UPDATE {serverRecord.Table} SET sync_status='conflict' WHERE id=$id AND school_id=$s";
            mark.Parameters.AddWithValue("$id", localId);
            mark.Parameters.AddWithValue("$s", schoolId);
            mark.ExecuteNonQuery();
        }

        // Satu konflik terbuka sahaja bagi rekod yang sama.
        using (var old = connection.CreateCommand())
        {
            old.CommandText = """
                UPDATE sync_conflicts
                SET resolved=1
                WHERE school_id=$s AND table_name=$t AND record_id=$r AND resolved=0
                """;
            old.Parameters.AddWithValue("$s", schoolId);
            old.Parameters.AddWithValue("$t", serverRecord.Table);
            old.Parameters.AddWithValue("$r", localId);
            old.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sync_conflicts(
                id, school_id, table_name, record_id,
                local_version, server_version, reason,
                server_payload, created_at, resolved)
            VALUES($id,$s,$t,$r,$lv,$sv,$why,$p,$n,0)
            """;
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$s", schoolId);
        cmd.Parameters.AddWithValue("$t", serverRecord.Table);
        cmd.Parameters.AddWithValue("$r", localId);
        cmd.Parameters.AddWithValue("$lv", localVersion);
        cmd.Parameters.AddWithValue("$sv", serverRecord.Version);
        cmd.Parameters.AddWithValue("$why", "Natural-key collision: UUID lokal dan server berbeza");
        cmd.Parameters.AddWithValue("$p", JsonSerializer.Serialize(serverRecord, json));
        cmd.Parameters.AddWithValue("$n", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private HashSet<string> GetWritableColumns(SqliteConnection connection, string table)
    {
        lock (writableColumnCache)
        {
            if (writableColumnCache.TryGetValue(table, out var cached)) return cached;

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(1);
                if (name is "id" or "school_id" or "version" or
                    "updated_at" or "deleted_at" or "sync_status")
                {
                    continue;
                }
                result.Add(name);
            }

            writableColumnCache[table] = result;
            return result;
        }
    }

    private void ValidateResponseTenant(SyncResponse response)
    {
        foreach (var record in response.Changes)
        {
            if (!string.Equals(record.SchoolId, DatabaseService.Instance.SchoolId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Server cuba menghantar data tenant/sekolah lain");
            }
        }

        foreach (var conflict in response.Conflicts)
        {
            if (conflict.Server is not null &&
                !string.Equals(conflict.Server.SchoolId, DatabaseService.Instance.SchoolId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Server cuba menghantar konflik tenant/sekolah lain");
            }
        }
    }

    private static object? ToDb(object? value)
    {
        if (value is not JsonElement element) return value;

        if (element.ValueKind == JsonValueKind.Null) return null;
        if (element.ValueKind == JsonValueKind.String) return element.GetString();
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetInt64(out var number) ? number : element.GetDouble();
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return element.GetBoolean() ? 1 : 0;

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("__type", out var type) &&
            type.GetString() == "base64")
        {
            var encoded = element.GetProperty("data").GetString() ?? "";
            return Convert.FromBase64String(encoded);
        }

        return element.ToString();
    }

    private static int Priority(string table) => table switch
    {
        "schools" => 0,
        "students" => 1,
        "teachers" => 2,
        "calendar_days" => 3,
        "attendance" => 4,
        "case_profiles" => 5,
        _ => 10
    };

    private static string TrimBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "tiada respons";
        var oneLine = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= 500 ? oneLine : oneLine[..500] + "…";
    }
}
