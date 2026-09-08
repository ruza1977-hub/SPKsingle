using System.Text.Json;
using System.Text.Json.Serialization;
namespace SistemPengurusanKehadiran.Models;

public sealed class SyncRecord
{
    [JsonPropertyName("table")] public string Table { get; set; } = "";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("school_id")] public string SchoolId { get; set; } = "";
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
    [JsonPropertyName("deleted_at")] public string? DeletedAt { get; set; }
    [JsonPropertyName("data")] public Dictionary<string, object?> Data { get; set; } = [];
}
public sealed class SyncAccepted { [JsonPropertyName("table")] public string Table { get; set; }=""; [JsonPropertyName("id")] public string Id {get;set;}=""; [JsonPropertyName("version")] public int Version {get;set;} }
public sealed class SyncConflict { [JsonPropertyName("table")] public string Table {get;set;}=""; [JsonPropertyName("id")] public string Id {get;set;}=""; [JsonPropertyName("reason")] public string Reason {get;set;}=""; [JsonPropertyName("server")] public SyncRecord? Server {get;set;} }
public sealed class SyncResponse
{
    [JsonPropertyName("protocol_version")] public string ProtocolVersion {get;set;}="1.0";
    [JsonPropertyName("server_time")] public string ServerTime {get;set;}="";
    [JsonPropertyName("accepted")] public List<SyncAccepted> Accepted {get;set;}=[];
    [JsonPropertyName("conflicts")] public List<SyncConflict> Conflicts {get;set;}=[];
    [JsonPropertyName("changes")] public List<SyncRecord> Changes {get;set;}=[];
    [JsonPropertyName("next_pull_revision")] public long NextPullRevision {get;set;}
    [JsonPropertyName("has_more")] public bool HasMore {get;set;}
}


public sealed class PendingSyncStat
{
    public string TableName { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public sealed class SyncConflictRow
{
    public string Id { get; set; } = "";
    public string TableName { get; set; } = "";
    public string Label { get; set; } = "";
    public string RecordId { get; set; } = "";
    public int LocalVersion { get; set; }
    public int ServerVersion { get; set; }
    public string Reason { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string CreatedText
    {
        get
        {
            return DateTimeOffset.TryParse(CreatedAt, out var value)
                ? value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : CreatedAt;
        }
    }
}
