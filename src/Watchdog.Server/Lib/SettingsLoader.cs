using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class SettingsLoader
{
    private static Lazy<WatchdogSettings> _lazy = CreateLazy();

    public static WatchdogSettings Get() => _lazy.Value;

    public static void Invalidate() =>
        _lazy = CreateLazy();

    private static Lazy<WatchdogSettings> CreateLazy() => new(() =>
    {
        if (!File.Exists(Paths.Settings)) return new WatchdogSettings();
        try
        {
            return JsonSerializer.Deserialize<WatchdogSettings>(
                File.ReadAllText(Paths.Settings), JsonOptions.Default) ?? new WatchdogSettings();
        }
        catch { return new WatchdogSettings(); }
    });
}
