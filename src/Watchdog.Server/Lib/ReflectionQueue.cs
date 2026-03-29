// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

/// <summary>
/// Persists the queue of nudges awaiting outcome evaluation.
/// Uses a single JSON array (not JSONL) — the full list is always read/written
/// atomically since partial reads would leave the queue inconsistent.
/// </summary>
public static class ReflectionQueue
{
    public static void Enqueue(PendingReflection reflection) => WatchdogDataStore.Current.EnqueueReflection(reflection);

    public static List<PendingReflection> GetDue(DateTimeOffset? asOf = null) =>
        WatchdogDataStore.Current.GetDueReflections(asOf);

    public static void Remove(string episodeId) => WatchdogDataStore.Current.RemoveReflection(episodeId);

    public static int Count() => WatchdogDataStore.Current.GetReflectionCount();

    public static List<PendingReflection> LoadAll() => WatchdogDataStore.Current.LoadAllReflections();
}
