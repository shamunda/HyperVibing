// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Diagnostics;
using System.Text.Json;

namespace Watchdog.Server.Lib;

public record LockInfo(int Pid, DateTimeOffset StartedAt);

public static class LockFile
{
    public static bool Acquire()
    {
        if (File.Exists(Paths.Lock))
        {
            var existing = Read();
            if (existing is not null && IsRunning(existing.Pid))
                return false; // live supervisor already running

            File.Delete(Paths.Lock); // stale lock
        }

        var info = new LockInfo(Environment.ProcessId, DateTimeOffset.UtcNow);
        File.WriteAllText(Paths.Lock, JsonSerializer.Serialize(info));
        return true;
    }

    public static void Release()
    {
        if (File.Exists(Paths.Lock)) File.Delete(Paths.Lock);
    }

    public static LockInfo? Read()
    {
        if (!File.Exists(Paths.Lock)) return null;
        try { return JsonSerializer.Deserialize<LockInfo>(File.ReadAllText(Paths.Lock)); }
        catch { return null; }
    }

    private static bool IsRunning(int pid)
    {
        try { Process.GetProcessById(pid); return true; }
        catch { return false; }
    }
}
