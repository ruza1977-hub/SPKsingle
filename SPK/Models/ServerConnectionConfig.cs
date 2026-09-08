using System.Text.Json.Serialization;
namespace SistemPengurusanKehadiran.Models;
public sealed class ServerConnectionConfig
{
    [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = "";
    [JsonPropertyName("api_path")] public string ApiPath { get; set; } = "/api/v1/sync";
    [JsonPropertyName("school_id")] public string SchoolId { get; set; } = "";
    [JsonPropertyName("environment")] public string EnvironmentName { get; set; } = "production";
    [JsonPropertyName("token")] public string Token { get; set; } = "";
}
