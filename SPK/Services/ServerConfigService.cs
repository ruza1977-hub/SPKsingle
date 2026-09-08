using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SistemPengurusanKehadiran.Helpers;
using SistemPengurusanKehadiran.Models;

namespace SistemPengurusanKehadiran.Services;

public sealed class ServerConfigService
{
    public static ServerConfigService Instance { get; } = new();

    public ServerConnectionConfig? Load()
    {
        if (!File.Exists(AppPaths.ServerConfig)) return null;
        try
        {
            return JsonSerializer.Deserialize<ServerConnectionConfig>(File.ReadAllText(AppPaths.ServerConfig));
        }
        catch
        {
            return null;
        }
    }

    public string? LoadToken()
    {
        if (!File.Exists(AppPaths.TokenFile)) return null;
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(File.ReadAllBytes(AppPaths.TokenFile), null, DataProtectionScope.CurrentUser));
        }
        catch
        {
            return null;
        }
    }

    public void SaveProvisioned(string baseUrl, string schoolId, string token)
    {
        var cfg = new ServerConnectionConfig
        {
            BaseUrl = baseUrl.TrimEnd('/'),
            ApiPath = "/api/v1/sync",
            SchoolId = schoolId,
            EnvironmentName = "production",
            Token = ""
        };
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ServerConfig)!);
        File.WriteAllText(AppPaths.ServerConfig, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllBytes(AppPaths.TokenFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser));
    }

    public bool IsReady(string localSchoolId, out string reason)
    {
        var cfg = Load();
        if (cfg is null)
        {
            reason = "Server Config belum tersedia";
            return false;
        }

        if (string.IsNullOrWhiteSpace(cfg.BaseUrl) ||
            !Uri.TryCreate(cfg.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            reason = "base_url Server Config tidak sah";
            return false;
        }

        if (string.IsNullOrWhiteSpace(cfg.SchoolId))
        {
            reason = "school_id Server Config tiada";
            return false;
        }

        if (!string.Equals(cfg.SchoolId, localSchoolId, StringComparison.OrdinalIgnoreCase))
        {
            reason = "School ID Server Config tidak sama dengan SQLite lokal";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LoadToken()))
        {
            reason = "Token Server Config tidak tersedia";
            return false;
        }

        reason = "Sedia untuk sync";
        return true;
    }

    public void ImportZip(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("server/server.json") ?? zip.GetEntry("server.json");
        if (entry is null) throw new InvalidDataException("server.json tiada");

        using var sr = new StreamReader(entry.Open());
        var cfg = JsonSerializer.Deserialize<ServerConnectionConfig>(sr.ReadToEnd());
        if (cfg is null) throw new InvalidDataException("Server config tidak sah");
        ValidateImportedConfig(cfg);

        // First provisioning: DB seed/local kosong menerima school_id daripada Server Config.
        // Selepas terikat, mismatch kekal ditolak untuk melindungi data sekolah lain.
        if (!DatabaseService.Instance.TryBindToServerSchool(cfg.SchoolId, out var bindReason))
            throw new InvalidDataException(bindReason);

        var token = cfg.Token;
        cfg.Token = "";
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ServerConfig)!);
        File.WriteAllText(
            AppPaths.ServerConfig,
            JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllBytes(
            AppPaths.TokenFile,
            ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser));
    }

    // Bootstrap pilihan: jika pengguna meletakkan ZIP sebenar bernama server_config.zip
    // di sebelah EXE atau dalam folder Config, aplikasi akan import sekali secara automatik.
    public string? TryAutoImportPackagedConfig()
    {
        if (Load() is not null && !string.IsNullOrWhiteSpace(LoadToken())) return null;

        foreach (var candidate in GetBootstrapCandidates())
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                ImportZip(candidate);
                return candidate;
            }
            catch
            {
                // Abaikan calon yang bukan pakej config sebenar. Import manual masih tersedia.
            }
        }
        return null;
    }

    private static IEnumerable<string> GetBootstrapCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "server_config.zip");
        yield return Path.Combine(baseDir, "Config", "server_config.zip");
        yield return Path.Combine(Environment.CurrentDirectory, "server_config.zip");
        yield return Path.Combine(Environment.CurrentDirectory, "Config", "server_config.zip");
    }

    private static void ValidateImportedConfig(ServerConnectionConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.BaseUrl) ||
            !Uri.TryCreate(cfg.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidDataException("base_url tidak sah");

        if (string.IsNullOrWhiteSpace(cfg.SchoolId))
            throw new InvalidDataException("school_id tiada");

        if (string.IsNullOrWhiteSpace(cfg.Token))
            throw new InvalidDataException("Token tiada");

        // Elak template contoh dianggap config sebenar.
        if (cfg.BaseUrl.Contains("YOUR-FASTAPI-SERVER", StringComparison.OrdinalIgnoreCase) ||
            cfg.SchoolId.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase) ||
            cfg.Token.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Ini masih Server Config template; isi nilai sebenar dahulu");
    }
}
