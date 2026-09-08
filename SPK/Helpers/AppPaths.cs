namespace SistemPengurusanKehadiran.Helpers;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SistemPengurusanKehadiran");

    public static string Data => Ensure("data");
    public static string Cache => Ensure("video_cache");

    // STANDARD CANONICAL (macOS): semua platform menggunakan nama pangkalan data yang sama.
    public static string Database => Path.Combine(Data, "sistem_pengurusan_kehadiran.sqlite3");

    // Hanya untuk migrasi sekali daripada Windows v0.5.2 dan lebih lama.
    public static string LegacyWindowsDatabase => Path.Combine(Data, "spk_windows.sqlite3");

    public static string ServerConfig => Path.Combine(Data, "server_config.json");
    public static string TokenFile => Path.Combine(Data, "sync_token.bin");
    public static string SettingsFile => Path.Combine(Data, "windows_settings.json");

    public static string Ensure(string folder)
    {
        var path = Path.Combine(Root, folder);
        Directory.CreateDirectory(path);
        return path;
    }
}
