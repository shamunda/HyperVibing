namespace Watchdog.Server.Lib;

public static class WatchdogDataStore
{
    private static readonly object Sync = new();
    private static IWatchdogDataStore? _current;

    public static IWatchdogDataStore Current
    {
        get
        {
            lock (Sync)
            {
                _current ??= new SqliteWatchdogDataStore();
                return _current;
            }
        }
    }

    public static void Configure(IWatchdogDataStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        lock (Sync)
        {
            _current = store;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _current = null;
        }
    }
}