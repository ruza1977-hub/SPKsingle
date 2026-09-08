using System.Text.RegularExpressions;

namespace SistemPengurusanKehadiran.Services;

public static class VideoLinkHelper
{
    public static (string Provider, string SourceId, string PreviewUrl, bool IsVertical) Parse(string? value)
    {
        var url = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url)) return ("UNKNOWN", "", "", false);

        if (TryYouTube(url, out var ytId, out var vertical))
            return ("YOUTUBE", ytId, $"https://www.youtube.com/embed/{ytId}", vertical);

        if (TryGoogleDrive(url, out var driveId))
            return ("GOOGLE_DRIVE", driveId, $"https://drive.google.com/file/d/{driveId}/preview", false);

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return ("URL", "", uri.ToString(), false);

        return ("UNKNOWN", "", "", false);
    }

    private static bool TryYouTube(string url, out string id, out bool vertical)
    {
        id = "";
        vertical = url.Contains("/shorts/", StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant();
        if (host is "youtu.be" or "www.youtu.be")
        {
            id = uri.AbsolutePath.Trim('/').Split('/')[0];
            return IsYouTubeId(id);
        }
        if (!host.EndsWith("youtube.com")) return false;

        var path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("shorts/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("embed/", StringComparison.OrdinalIgnoreCase))
        {
            id = path.Split('/').Skip(1).FirstOrDefault() ?? "";
            return IsYouTubeId(id);
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                id = Uri.UnescapeDataString(kv[1]);
                return IsYouTubeId(id);
            }
        }
        return false;
    }

    private static bool TryGoogleDrive(string url, out string id)
    {
        id = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Host.EndsWith("drive.google.com", StringComparison.OrdinalIgnoreCase)) return false;

        var match = Regex.Match(uri.AbsolutePath, @"/file/d/([^/]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            id = match.Groups[1].Value;
            return !string.IsNullOrWhiteSpace(id);
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                id = Uri.UnescapeDataString(kv[1]);
                return !string.IsNullOrWhiteSpace(id);
            }
        }
        return false;
    }

    private static bool IsYouTubeId(string id) => Regex.IsMatch(id ?? "", @"^[A-Za-z0-9_-]{6,20}$");
}
