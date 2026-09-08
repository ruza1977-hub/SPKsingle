namespace SistemPengurusanKehadiran.Services;

public sealed class AutoSyncEventArgs : EventArgs
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public SyncRunResult? Result { get; init; }
}

public sealed class AutoSyncService
{
    public static AutoSyncService Instance { get; } = new();

    private System.Threading.Timer? timer;
    private int running;
    private readonly object gate = new();

    public event EventHandler<AutoSyncEventArgs>? SyncFinished;

    public bool Enabled => string.Equals(
        DatabaseService.Instance.GetSyncState("auto_sync_enabled"),
        "1",
        StringComparison.Ordinal);

    public int IntervalMinutes
    {
        get
        {
            return int.TryParse(DatabaseService.Instance.GetSyncState("auto_sync_interval_minutes"), out var value)
                ? NormalizeInterval(value)
                : 15;
        }
    }

    // Dipanggil semasa aplikasi mula. Jika Server Config sah sudah wujud,
    // Auto Sync diaktifkan secara default hanya sekali. Pilihan pengguna selepas itu dihormati.
    public void InitializeFromServerConfig()
    {
        var initialized = DatabaseService.Instance.GetSyncState("auto_sync_preference_initialized");
        if (!string.IsNullOrWhiteSpace(initialized)) return;

        if (ServerConfigService.Instance.IsReady(DatabaseService.Instance.SchoolId, out _))
        {
            DatabaseService.Instance.SetSyncState("auto_sync_enabled", "1");
            DatabaseService.Instance.SetSyncState("auto_sync_interval_minutes", "15");
            DatabaseService.Instance.SetSyncState("auto_sync_preference_initialized", "1");
        }
    }

    public void Start() => Reschedule();

    public void Stop()
    {
        lock (gate)
        {
            timer?.Dispose();
            timer = null;
        }
    }

    public void SetEnabled(bool enabled)
    {
        DatabaseService.Instance.SetSyncState("auto_sync_enabled", enabled ? "1" : "0");
        DatabaseService.Instance.SetSyncState("auto_sync_preference_initialized", "1");
        Reschedule();
    }

    public void SetIntervalMinutes(int minutes)
    {
        minutes = NormalizeInterval(minutes);
        DatabaseService.Instance.SetSyncState("auto_sync_interval_minutes", minutes.ToString());
        Reschedule();
    }

    // Digunakan selepas import Server Config supaya pengguna tidak perlu tekan Uji Server/Sync lagi.
    public Task TriggerNowAsync() => RunOnceAsync();

    private void Reschedule()
    {
        lock (gate)
        {
            timer?.Dispose();
            timer = null;

            if (!Enabled) return;

            var interval = TimeSpan.FromMinutes(IntervalMinutes);
            timer = new System.Threading.Timer(
                state => { _ = RunOnceAsync(); },
                null,
                TimeSpan.FromSeconds(3),
                interval);
        }
    }

    private async Task RunOnceAsync()
    {
        if (Interlocked.Exchange(ref running, 1) == 1) return;

        try
        {
            if (!ServerConfigService.Instance.IsReady(DatabaseService.Instance.SchoolId, out var reason))
            {
                DatabaseService.Instance.SetSyncState("last_auto_sync_error", reason);
                Raise(false, reason, null);
                return;
            }

            var result = await RailwaySyncService.Instance.SyncAsync();
            var now = DateTime.UtcNow.ToString("o");
            DatabaseService.Instance.SetSyncState("last_auto_sync_at", now);
            DatabaseService.Instance.SetSyncState("last_auto_sync_error", "");
            Raise(true, "Selesai", result);
        }
        catch (Exception ex)
        {
            DatabaseService.Instance.SetSyncState("last_auto_sync_error", ex.Message);
            Raise(false, ex.Message, null);
        }
        finally
        {
            Interlocked.Exchange(ref running, 0);
        }
    }

    private void Raise(bool success, string message, SyncRunResult? result)
    {
        try
        {
            SyncFinished?.Invoke(this, new AutoSyncEventArgs
            {
                Success = success,
                Message = message,
                Result = result
            });
        }
        catch
        {
            // Event UI tidak boleh mematikan enjin Auto Sync.
        }
    }

    private static int NormalizeInterval(int minutes)
        => minutes switch
        {
            <= 5 => 5,
            <= 15 => 15,
            <= 30 => 30,
            _ => 60
        };
}
