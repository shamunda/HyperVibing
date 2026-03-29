// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Lib;

public record LockInfo(int Pid, DateTimeOffset StartedAt);

public static class LockFile
{
    public static bool Acquire()
        => WatchdogDataStore.Current.TryAcquireLease("watch-loop", Environment.ProcessId, TimeSpan.FromMinutes(2));

    public static void Release() => WatchdogDataStore.Current.ReleaseLease("watch-loop", Environment.ProcessId);

    public static LockInfo? Read() => WatchdogDataStore.Current.ReadLease("watch-loop");
}
