using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SistemPengurusanKehadiran.Models;

namespace SistemPengurusanKehadiran.Services;

public sealed class ProvisioningResult
{
    public string SchoolId { get; set; } = "";
    public string? TeacherId { get; set; }
    public string DeviceId { get; set; } = "";
    public string DeviceToken { get; set; } = "";
}

public sealed class ProvisioningService
{
    public const string ProductionBaseUrl = "https://fail-kes-ponteng-production.up.railway.app";
    public static ProvisioningService Instance { get; } = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public string DeviceId
    {
        get
        {
            var id = DatabaseService.Instance.GetSyncState("device_id");
            if (!string.IsNullOrWhiteSpace(id)) return id;
            id = Guid.NewGuid().ToString();
            DatabaseService.Instance.SetSyncState("device_id", id);
            return id;
        }
    }

    public bool IsProvisioned => ServerConfigService.Instance.IsReady(DatabaseService.Instance.SchoolId, out _);

    public async Task<ProvisioningResult> ActivateAsync(string activationCode, CancellationToken cancellationToken = default)
    {
        var normalized = (activationCode ?? "").Trim().ToUpperInvariant();
        if (normalized.Length < 6) throw new InvalidOperationException("Masukkan kod pengaktifan yang sah.");

        var payload = new
        {
            activation_code = normalized,
            device_id = DeviceId,
            platform = "windows",
            device_name = Environment.MachineName
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, ProductionBaseUrl + "/api/v1/provision/activate");
        req.Content = new StringContent(JsonSerializer.Serialize(payload, json), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)resp.StatusCode}: {ReadDetail(text)}");

        var result = JsonSerializer.Deserialize<ProvisioningResult>(text, json)
            ?? throw new InvalidDataException("Respons provisioning tidak sah.");
        if (string.IsNullOrWhiteSpace(result.SchoolId) || string.IsNullOrWhiteSpace(result.DeviceToken))
            throw new InvalidDataException("Server tidak memulangkan credential peranti lengkap.");

        if (!DatabaseService.Instance.TryBindToServerSchool(result.SchoolId, out var reason))
            throw new InvalidOperationException(reason);

        ServerConfigService.Instance.SaveProvisioned(
            ProductionBaseUrl,
            result.SchoolId,
            result.DeviceToken);

        DatabaseService.Instance.SetSyncState("teacher_id", result.TeacherId ?? "");
        DatabaseService.Instance.SetSyncState("provisioning_mode", "device_token");
        DatabaseService.Instance.SetSyncState("provisioned_at", DateTime.UtcNow.ToString("o"));

        AutoSyncService.Instance.InitializeFromServerConfig();
        AutoSyncService.Instance.SetEnabled(true);
        return result;
    }

    private static string ReadDetail(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var d)) return d.ToString();
        }
        catch { }
        return string.IsNullOrWhiteSpace(body) ? "Ralat server" : body.Length > 180 ? body[..180] : body;
    }
}
