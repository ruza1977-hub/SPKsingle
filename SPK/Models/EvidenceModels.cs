namespace SistemPengurusanKehadiran.Models;

public sealed class EvidenceFileRecord
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime EvidenceDate { get; set; } = DateTime.Today;
    public string EvidenceType { get; set; } = "DOKUMEN";
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string Notes { get; set; } = "";
    public byte[] FileData { get; set; } = Array.Empty<byte>();
    public string SyncStatus { get; set; } = "pending";
    public string DateText => EvidenceDate.ToString("dd/MM/yyyy");
    public string FileSizeText => FileSize switch
    {
        >= 1024L * 1024L => $"{FileSize / 1024d / 1024d:0.0} MB",
        >= 1024L => $"{FileSize / 1024d:0.0} KB",
        _ => $"{FileSize} B"
    };
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? FileName : Title;
}

public sealed class VideoEvidenceRecord
{
    public string Id { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Kelas { get; set; } = "";
    public DateTime EvidenceDate { get; set; } = DateTime.Today;
    public string Provider { get; set; } = "UNKNOWN";
    public string SourceUrl { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string SyncStatus { get; set; } = "pending";
    public string DateText => EvidenceDate.ToString("dd/MM/yyyy");
    public string ProviderDisplay => Provider switch
    {
        "YOUTUBE" => "YouTube",
        "GOOGLE_DRIVE" => "Google Drive",
        "LOCAL" => "Video Lokal",
        _ => "Pautan Video"
    };
}
